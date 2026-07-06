using System.Data;
using Dapper;
using GymAffiliate.Domain.Interfaces.Repositories;
using GymAffiliate.Infrastructure.Persistence.Dapper.Context;
using GymAffiliate.Infrastructure.Persistence.Dapper.Parameters;
using GymAffiliate.Shared.Constants;
using GymAffiliate.Shared.Errors;
using GymAffiliate.Shared.Result;
using Microsoft.Extensions.Logging;

namespace GymAffiliate.Infrastructure.Persistence.Repositories;

// ─────────────────────────────────────────────────────────────────────────────
// SpHelper
//
// COMPORTAMIENTO REAL DE LOS SPs:
//
// CAMINO EXITOSO (sin error):
//   RS1: datos del registro creado/actualizado  (afiliado, membresía, pago…)
//   RS2: SELECT EndProcedure → ErrorId=0, ErrorMessage=NULL
//
// CAMINO DE ERROR (GOTO EndProcedure anticipado):
//   RS1 ÚNICO: SELECT EndProcedure → ErrorId>0, ErrorMessage=texto del error
//   (NO hay RS2 — por eso el GridReader se disposa al intentar leerlo)
//
// SOLUCIÓN: leer siempre el PRIMER RS como dynamic.
// Detectar si es el "output row" (tiene campo ErrorId) o datos reales.
// Si es output row → hubo error anticipado. Si no → leer RS2 como output row.
// ─────────────────────────────────────────────────────────────────────────────
file static class SpHelper
{
    /// <summary>
    /// Lee el resultado de un SP de escritura que puede retornar:
    ///   - 2 RS (éxito):  RS1=datos, RS2=output SELECT
    ///   - 1 RS (error):  RS1=output SELECT con ErrorId > 0
    ///
    /// Retorna (outputRow, dataRow).
    /// dataRow es null cuando el SP tomó el camino de error.
    /// </summary>
    public static async Task<(SpOutputRow? Output, IDictionary<string, object>? DataRow)>
        ReadWriteResultAsync(SqlMapper.GridReader multi)
    {
        // Leer el primer RS como dynamic para inspeccionar
        var firstRows = (await multi.ReadAsync<dynamic>()).ToList();
        var first = firstRows.FirstOrDefault() as IDictionary<string, object>;

        if (first is null)
        {
            // SP no devolvió nada — intentar leer el output de todos modos
            var outputSolo = await TryReadOutputAsync(multi);
            return (outputSolo, null);
        }

        // ¿El primer RS es el output row? → tiene "ErrorId" como campo
        if (first.ContainsKey("ErrorId"))
        {
            // Camino de error: solo hay 1 RS y es el output
            var outputRow = MapToOutputRow(first);
            return (outputRow, null);
        }

        // Camino exitoso: RS1 = datos, RS2 = output
        var output = await TryReadOutputAsync(multi);
        return (output, first);
    }

    /// <summary>Lee el output SELECT (último RS) de forma segura.</summary>
    public static async Task<SpOutputRow?> TryReadOutputAsync(SqlMapper.GridReader multi)
    {
        try
        {
            if (multi.IsConsumed) return null;
            return await multi.ReadFirstOrDefaultAsync<SpOutputRow>();
        }
        catch
        {
            return null;
        }
    }

    private static SpOutputRow MapToOutputRow(IDictionary<string, object> row)
    {
        int? errorId = row.TryGetValue("ErrorId", out var eid) && eid is not null
            ? Convert.ToInt32(eid)
            : null;

        string? errorMsg = row.TryGetValue("ErrorMessage", out var emsg) && emsg is not null
            ? emsg.ToString()
            : null;

        DateTime? opDate = row.TryGetValue("OperationDate", out var od) && od is not null
            ? Convert.ToDateTime(od)
            : DateTime.UtcNow;

        string? opType = row.TryGetValue("OperationType", out var ot) && ot is not null
            ? ot.ToString()
            : null;

        return new SpOutputRow
        {
            OperationType = opType,
            ErrorId = errorId == 0 ? null : errorId,
            ErrorMessage = errorMsg,
            OperationDate = opDate
        };
    }

    public static (int? ErrorId, string? ErrorMsg) GetError(SpOutputRow? output)
    {
        if (output is null) return (null, null);
        var eid = output.ErrorId == 0 ? null : output.ErrorId;
        return (eid, output.ErrorMessage);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// AfiliadoRepository — sp_Affiliates
// ─────────────────────────────────────────────────────────────────────────────
public sealed class AfiliadoRepository(
    IDapperContext db,
    ILogger<AfiliadoRepository> log) : IAfiliadoRepository
{
    public async Task<Result<int>> CrearAsync(
        CrearAfiliadoParams p, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm = SpParameters.Crear(p);
            using var multi = await conn.QueryMultipleAsync(
                StoredProcedures.Affiliates, prm, commandType: CommandType.StoredProcedure);

            var (output, dataRow) = await SpHelper.ReadWriteResultAsync(multi);
            var (eid, emsg) = SpHelper.GetError(output);

            if (eid.HasValue)
            {
                log.LogWarning("CrearAfiliado ErrorId={E} Msg={M}", eid, emsg);
                return Result<int>.Failure(SpErrorMapper.ToResultError(eid.Value, emsg ?? "Error al crear afiliado."));
            }

            // dataRow contiene el afiliado creado
            int newId = dataRow is not null && dataRow.TryGetValue("AffiliateId", out var aid)
                        ? Convert.ToInt32(aid) : 0;

            log.LogInformation("Afiliado creado Id={Id}", newId);
            return Result<int>.Success(newId);
        }, ct);

    public async Task<Result<int>> ActualizarAsync(
        ActualizarAfiliadoParams p, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm = SpParameters.Actualizar(p);
            using var multi = await conn.QueryMultipleAsync(
                StoredProcedures.Affiliates, prm, commandType: CommandType.StoredProcedure);

            var (output, _) = await SpHelper.ReadWriteResultAsync(multi);
            var (eid, emsg) = SpHelper.GetError(output);

            if (eid.HasValue)
                return Result<int>.Failure(SpErrorMapper.ToResultError(eid.Value, emsg ?? "Error al actualizar."));

            return Result<int>.Success(p.AffiliateId);
        }, ct);

    public async Task<Result> EliminarAsync(
        int affiliateId, string? notes,
        int? userId, string? ip, string? session,
        CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm = SpParameters.Eliminar(affiliateId, notes, userId, ip, session);
            using var multi = await conn.QueryMultipleAsync(
                StoredProcedures.Affiliates, prm, commandType: CommandType.StoredProcedure);

            var (output, _) = await SpHelper.ReadWriteResultAsync(multi);
            var (eid, emsg) = SpHelper.GetError(output);

            if (eid.HasValue)
                return Result.Failure(SpErrorMapper.ToResultError(eid.Value, emsg ?? "Error al eliminar."));

            log.LogInformation("Afiliado {Id} dado de baja.", affiliateId);
            return Result.Success();
        }, ct);

    // viewaffiliated: RS1=afiliado | RS2=membresías | RS3=pagos | RS4=output
    // En error: RS1 ÚNICO = output SELECT
    public async Task<Result<AfiliadoDetalleRaw?>> ObtenerAsync(
        int? id, string? doc, string? email,
        int? userId, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm = SpParameters.Ver(id, doc, email, userId);
            using var multi = await conn.QueryMultipleAsync(
                StoredProcedures.Affiliates, prm, commandType: CommandType.StoredProcedure);

            // Leer primer RS: puede ser el afiliado O el output de error
            var firstRows = (await multi.ReadAsync<dynamic>()).ToList();
            var first = firstRows.FirstOrDefault() as IDictionary<string, object>;

            // Si el primer RS tiene ErrorId → es el camino de error
            //if (first is not null && first.ContainsKey("ErrorId"))
            //{
            //    //var errOutput = SpHelper.TryReadOutputAsync(multi);

            //    var errOutput = await SpHelper.TryReadOutputAsync(multi);

            //    var errRow    = new SpOutputRow(null,
            //        first.TryGetValue("ErrorId",      out var e) && e is not null ? Convert.ToInt32(e) : null,
            //        first.TryGetValue("ErrorMessage", out var m) && m is not null ? m.ToString()       : null,
            //        DateTime.UtcNow);
            //    var (eid2, emsg2) = SpHelper.GetError(errRow);
            //    if (eid2.HasValue)
            //        return Result<AfiliadoDetalleRaw?>.Failure(
            //            SpErrorMapper.ToResultError(eid2.Value, emsg2 ?? "Afiliado no encontrado."));

            //    return Result<AfiliadoDetalleRaw?>.Success(null);
            //}
            if (first is not null && first.ContainsKey("ErrorId"))
            {
                var output = new SpOutputRow
                {
                    OperationType = first.TryGetValue("OperationType", out var ot) ? ot?.ToString() : null,
                    ErrorId = first.TryGetValue("ErrorId", out var e) && e is not null ? Convert.ToInt32(e) : null,
                    ErrorMessage = first.TryGetValue("ErrorMessage", out var m) ? m?.ToString() : null,
                    OperationDate = DateTime.UtcNow
                };

                var (eid2, emsg2) = SpHelper.GetError(output);

                if (eid2.HasValue)
                    return Result<AfiliadoDetalleRaw?>.Failure(
                        SpErrorMapper.ToResultError(eid2.Value, emsg2 ?? "Afiliado no encontrado."));

                return Result<AfiliadoDetalleRaw?>.Success(null);
            }

            // Camino exitoso: RS1 leído, consumir RS2 y RS3, luego leer RS4 (output)
            //_ = await multi.ReadAsync<MembresiaHistorialRaw>();
            //_ = await multi.ReadAsync<PagoHistorialRaw>();
            _ = (await multi.ReadAsync<MembresiaHistorialRaw>()).ToList();
            _ = (await multi.ReadAsync<PagoHistorialRaw>()).ToList();
            _ = await SpHelper.TryReadOutputAsync(multi);

            // Reconstruir AfiliadoDetalleRaw desde el primer RS
            if (first is null) return Result<AfiliadoDetalleRaw?>.Success(null);

            var afiliado = MapAfiliado(first);
            return Result<AfiliadoDetalleRaw?>.Success(afiliado);
        }, ct);

    // listaffiliated: UN solo RS con filas + TotalRecords por COUNT(*) OVER()
    public async Task<Result<(IEnumerable<AfiliadoListaRaw> Items, int Total)>> ListarAsync(
        ListarAfiliadosParams p, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm  = SpParameters.Listar(p);
            var rows = (await conn.QueryAsync<AfiliadoListaRaw>(
                StoredProcedures.Affiliates, prm,
                commandType: CommandType.StoredProcedure)).ToList();

            var total = rows.FirstOrDefault()?.TotalRecords ?? 0;
            return Result<(IEnumerable<AfiliadoListaRaw>, int)>.Success((rows, total));
        }, ct);

    private static AfiliadoDetalleRaw MapAfiliado(IDictionary<string, object> r)
    {
        T Get<T>(string key, T def = default!) =>
            r.TryGetValue(key, out var v) && v is not null
                ? (T)Convert.ChangeType(v, typeof(T))
                : def;

        return new AfiliadoDetalleRaw
        {
            AffiliateId       = Get<int>("AffiliateId"),
            DocumentNumber    = Get<string>("DocumentNumber") ?? "",
            DocumentType      = Get<string>("DocumentType")  ?? "",
            FirstName         = Get<string>("FirstName")     ?? "",
            LastName          = Get<string>("LastName")      ?? "",
            Age               = Get<int>("Age"),
            BirthDate         = Get<DateTime>("BirthDate"),
            Phone             = r.TryGetValue("Phone",  out var ph) && ph is not null ? ph.ToString() : null,
            Email             = Get<string>("Email") ?? "",
            Address           = r.TryGetValue("Address", out var ad) && ad is not null ? ad.ToString() : null,
            EmergencyContact  = r.TryGetValue("EmergencyContact", out var ec) && ec is not null ? ec.ToString() : null,
            EmergencyPhone    = r.TryGetValue("EmergencyPhone",   out var ep) && ep is not null ? ep.ToString() : null,
            BaseBranchId      = r.TryGetValue("BaseBranchId",     out var bb) && bb is not null ? Convert.ToInt32(bb) : null,
            BaseBranchName    = r.TryGetValue("BaseBranchName",   out var bn) && bn is not null ? bn.ToString() : null,
            StatusId          = Get<byte>("StatusId"),
            StatusName        = Get<string>("StatusName") ?? "",
            RegistrationDate  = Get<DateTime>("RegistrationDate"),
            Notes             = r.TryGetValue("Notes", out var no) && no is not null ? no.ToString() : null,
            MembershipId      = r.TryGetValue("MembershipId", out var mid) && mid is not null ? Convert.ToInt32(mid) : null,
            TypeCode          = r.TryGetValue("TypeCode",          out var tc) && tc is not null ? tc.ToString() : null,
            MembershipTypeName= r.TryGetValue("MembershipTypeName",out var mtn)&& mtn is not null? mtn.ToString(): null,
            AccessScope       = r.TryGetValue("AccessScope",       out var ac) && ac is not null ? ac.ToString() : null,
            StartDate         = r.TryGetValue("StartDate",         out var sd) && sd is not null ? Convert.ToDateTime(sd) : null,
            EndDate           = r.TryGetValue("EndDate",           out var ed) && ed is not null ? Convert.ToDateTime(ed) : null,
            DaysUntilExpiry   = r.TryGetValue("DaysUntilExpiry",   out var du) && du is not null ? Convert.ToInt32(du)   : null,
            MembershipBranchId= r.TryGetValue("MembershipBranchId",out var mb) && mb is not null ? Convert.ToInt32(mb)   : null,
            MembershipBranchName=r.TryGetValue("MembershipBranchName",out var mbn)&&mbn is not null?mbn.ToString():null,
            RenewalCount      = r.TryGetValue("RenewalCount",      out var rc) && rc is not null ? Convert.ToInt32(rc)   : null,
            LastPaymentId     = r.TryGetValue("LastPaymentId",     out var lp) && lp is not null ? Convert.ToInt32(lp)   : null,
            LastPaymentAmount = r.TryGetValue("LastPaymentAmount", out var la) && la is not null ? Convert.ToDecimal(la) : null,
            LastPaymentDate   = r.TryGetValue("LastPaymentDate",   out var ld) && ld is not null ? Convert.ToDateTime(ld): null,
            LastPaymentMethod = r.TryGetValue("LastPaymentMethod", out var lm) && lm is not null ? lm.ToString()         : null,
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// MembresiaRepository — sp_Memberships
// ─────────────────────────────────────────────────────────────────────────────
public sealed class MembresiaRepository(
    IDapperContext db,
    ILogger<MembresiaRepository> log) : IMembresiaRepository
{
    private async Task<Result<int>> RunWriteAsync(
        DynamicParameters prm, IDbConnection conn, string label)
    {
        using var multi = await conn.QueryMultipleAsync(
            StoredProcedures.Memberships, prm, commandType: CommandType.StoredProcedure);

        var (output, dataRow) = await SpHelper.ReadWriteResultAsync(multi);
        var (eid, emsg) = SpHelper.GetError(output);

        if (eid.HasValue)
        {
            log.LogWarning("{Label} ErrorId={E}", label, eid);
            return Result<int>.Failure(SpErrorMapper.ToResultError(eid.Value, emsg ?? "Error."));
        }

        int newId = dataRow is not null && dataRow.TryGetValue("MembershipId", out var mid)
                    ? Convert.ToInt32(mid) : 0;
        log.LogInformation("{Label} MembershipId={Id}", label, newId);
        return Result<int>.Success(newId);
    }

    public async Task<Result<int>> AsignarAsync(AsignarMembresiaParams p, CancellationToken ct = default) =>
        await db.ExecuteAsync(conn => RunWriteAsync(SpParameters.Asignar(p), conn, "Asignar"), ct);

    public async Task<Result<int>> RenovarAsync(RenovarMembresiaParams p, CancellationToken ct = default) =>
        await db.ExecuteAsync(conn => RunWriteAsync(SpParameters.Renovar(p), conn, "Renovar"), ct);

    public async Task<Result<int>> CambiarPlanAsync(CambiarPlanParams p, CancellationToken ct = default) =>
        await db.ExecuteAsync(conn => RunWriteAsync(SpParameters.CambiarPlan(p), conn, "CambiarPlan"), ct);

    public async Task<Result> CancelarAsync(
        int? membershipId, int? affiliateId, string? notes,
        int? userId, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm = SpParameters.Cancelar(membershipId, affiliateId, notes, userId);
            using var multi = await conn.QueryMultipleAsync(
                StoredProcedures.Memberships, prm, commandType: CommandType.StoredProcedure);

            var (output, _) = await SpHelper.ReadWriteResultAsync(multi);
            var (eid, emsg) = SpHelper.GetError(output);
            return eid.HasValue
                ? Result.Failure(SpErrorMapper.ToResultError(eid.Value, emsg ?? "Error al cancelar."))
                : Result.Success();
        }, ct);

    public async Task<Result<ValidacionAccesoRaw?>> ValidarActivaAsync(
        int affiliateId, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm = SpParameters.Validar(affiliateId);
            var row = await conn.QueryFirstOrDefaultAsync<ValidacionAccesoRaw>(
                StoredProcedures.Memberships, prm, commandType: CommandType.StoredProcedure);
            return Result<ValidacionAccesoRaw?>.Success(row);
        }, ct);
}

// ─────────────────────────────────────────────────────────────────────────────
// PagoRepository — sp_Payments
// ─────────────────────────────────────────────────────────────────────────────
public sealed class PagoRepository(
    IDapperContext db,
    ILogger<PagoRepository> log) : IPagoRepository
{
    public async Task<Result<(int PaymentId, string Receipt)>> RegistrarAsync(
        RegistrarPagoParams p, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm = SpParameters.RegistrarPago(p);
            using var multi = await conn.QueryMultipleAsync(
                StoredProcedures.Payments, prm, commandType: CommandType.StoredProcedure);

            var (output, dataRow) = await SpHelper.ReadWriteResultAsync(multi);
            var (eid, emsg) = SpHelper.GetError(output);

            if (eid.HasValue)
                return Result<(int, string)>.Failure(
                    SpErrorMapper.ToResultError(eid.Value, emsg ?? "Error al registrar pago."));

            int    pid = dataRow is not null && dataRow.TryGetValue("PaymentId",      out var pi) ? Convert.ToInt32(pi) : 0;
            string rec = dataRow is not null && dataRow.TryGetValue("ReceiptNumber",  out var rn) ? rn?.ToString() ?? "" : "";

            log.LogInformation("Pago registrado Id={Id} Comprobante={R}", pid, rec);
            return Result<(int, string)>.Success((pid, rec));
        }, ct);

    public async Task<Result> CancelarAsync(
        int paymentId, string? notes,
        int? userId, string? ip, string? session,
        CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm = SpParameters.CancelarPago(paymentId, notes, userId, ip, session);
            using var multi = await conn.QueryMultipleAsync(
                StoredProcedures.Payments, prm, commandType: CommandType.StoredProcedure);

            var (output, _) = await SpHelper.ReadWriteResultAsync(multi);
            var (eid, emsg) = SpHelper.GetError(output);
            return eid.HasValue
                ? Result.Failure(SpErrorMapper.ToResultError(eid.Value, emsg ?? "Error al cancelar pago."))
                : Result.Success();
        }, ct);

    public async Task<Result<IEnumerable<PagoListaRaw>>> ListarAsync(
        int? affiliateId, DateOnly? from, DateOnly? to,
        int? branchId, int? userId, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm  = SpParameters.ListarPagos(affiliateId, from, to, branchId, userId);
            var rows = await conn.QueryAsync<PagoListaRaw>(
                StoredProcedures.Payments, prm, commandType: CommandType.StoredProcedure);
            return Result<IEnumerable<PagoListaRaw>>.Success(rows);
        }, ct);
}

// ─────────────────────────────────────────────────────────────────────────────
// AccesoRepository — sp_CheckIn
// ─────────────────────────────────────────────────────────────────────────────
public sealed class AccesoRepository(
    IDapperContext db,
    ILogger<AccesoRepository> log) : IAccesoRepository
{
    public async Task<Result<CheckInRaw>> RegistrarIngresoAsync(
        int affiliateId, int branchId,
        int? userId, string? ip, string? session,
        CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm = SpParameters.RegistrarCheckIn(affiliateId, branchId, userId, ip, session);
            using var multi = await conn.QueryMultipleAsync(
                StoredProcedures.CheckIn, prm, commandType: CommandType.StoredProcedure);

            var (output, dataRow) = await SpHelper.ReadWriteResultAsync(multi);
            var (eid, emsg) = SpHelper.GetError(output);

            // Error de sistema real (afilado no encontrado, sucursal no existe)
            if (eid.HasValue && dataRow is null)
                return Result<CheckInRaw>.Failure(
                    SpErrorMapper.ToResultError(eid.Value, emsg ?? "Error al registrar ingreso."));

            // Acceso denegado es resultado válido (AccessGranted=false), no error de sistema
            if (dataRow is null)
                return Result<CheckInRaw>.Failure("SY_901", "El SP no devolvió resultado.", 500);

            var row = new CheckInRaw
            {
                CheckInId          = dataRow.TryGetValue("CheckInId",         out var ci) && ci is not null ? Convert.ToInt32(ci)    : null,
                AffiliateId        = dataRow.TryGetValue("AffiliateId",       out var ai) && ai is not null ? Convert.ToInt32(ai)    : 0,
                AffiliateName      = dataRow.TryGetValue("AffiliateName",     out var an) && an is not null ? an.ToString() ?? ""    : "",
                BranchId           = dataRow.TryGetValue("BranchId",          out var bi) && bi is not null ? Convert.ToInt32(bi)    : 0,
                AccessGranted      = dataRow.TryGetValue("AccessGranted",     out var ag) && ag is not null && Convert.ToBoolean(ag),
                DenialReason       = dataRow.TryGetValue("DenialReason",      out var dr) && dr is not null ? dr.ToString()          : null,
                MembershipEndDate  = dataRow.TryGetValue("MembershipEndDate", out var me) && me is not null ? Convert.ToDateTime(me) : null,
                CheckInTime        = dataRow.TryGetValue("CheckInTime",       out var ct2)&& ct2 is not null? Convert.ToDateTime(ct2): DateTime.UtcNow,
                ErrorId            = eid
            };

            log.LogInformation("CheckIn AffId={A} Branch={B} Granted={G}",
                affiliateId, branchId, row.AccessGranted);

            return Result<CheckInRaw>.Success(row);
        }, ct);

    public async Task<Result<ValidacionAccesoRaw?>> ValidarAccesoAsync(
        int affiliateId, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm = SpParameters.Validar(affiliateId);
            var row = await conn.QueryFirstOrDefaultAsync<ValidacionAccesoRaw>(
                StoredProcedures.Memberships, prm, commandType: CommandType.StoredProcedure);
            return Result<ValidacionAccesoRaw?>.Success(row);
        }, ct);
}

// ─────────────────────────────────────────────────────────────────────────────
// NotificacionRepository — sp_Notifications + sp_Reports
// ─────────────────────────────────────────────────────────────────────────────
public sealed class NotificacionRepository(IDapperContext db) : INotificacionRepository
{
    public async Task<Result<IEnumerable<VencimientoRaw>>> ObtenerPorVencerAsync(
        int daysAhead, int? userId, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm = new DynamicParameters();
            prm.Add("@OperationType",    "expiringmemberships");
            prm.Add("@DaysAhead",        daysAhead);
            prm.Add("@ExecutedByUserId", userId);
            var rows = await conn.QueryAsync<VencimientoRaw>(
                StoredProcedures.Reports, prm, commandType: CommandType.StoredProcedure);
            return Result<IEnumerable<VencimientoRaw>>.Success(rows);
        }, ct);

    public async Task<Result<IEnumerable<NotificacionPendienteRaw>>> ObtenerPendientesAsync(
        int? affiliateId, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm  = SpParameters.ListarPendientes(affiliateId);
            var rows = await conn.QueryAsync<NotificacionPendienteRaw>(
                StoredProcedures.Notifications, prm, commandType: CommandType.StoredProcedure);
            return Result<IEnumerable<NotificacionPendienteRaw>>.Success(rows);
        }, ct);

    public async Task<Result<int>> GenerarAlertasAsync(
        int daysAhead, string channel,
        int? userId, string? session,
        CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm = SpParameters.GenerarAlertas(daysAhead, channel, userId, session);
            using var multi = await conn.QueryMultipleAsync(
                StoredProcedures.Notifications, prm, commandType: CommandType.StoredProcedure);

            var (output, dataRow) = await SpHelper.ReadWriteResultAsync(multi);
            var (eid, emsg) = SpHelper.GetError(output);
            if (eid.HasValue)
                return Result<int>.Failure(
                    SpErrorMapper.ToResultError(eid.Value, emsg ?? "Error al generar notificaciones."));

            int created = dataRow is not null && dataRow.TryGetValue("NotificationsGenerated", out var ng)
                          ? Convert.ToInt32(ng) : 0;
            return Result<int>.Success(created);
        }, ct);

    public async Task<Result> MarcarEnviadaAsync(
        int notificationId, string? errorDetail,
        int? userId, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm = SpParameters.MarcarEnviada(notificationId, errorDetail, userId);
            using var multi = await conn.QueryMultipleAsync(
                StoredProcedures.Notifications, prm, commandType: CommandType.StoredProcedure);

            var (output, _) = await SpHelper.ReadWriteResultAsync(multi);
            var (eid, emsg) = SpHelper.GetError(output);
            return eid.HasValue
                ? Result.Failure(SpErrorMapper.ToResultError(eid.Value, emsg ?? "Error al marcar notificación."))
                : Result.Success();
        }, ct);
}

// ─────────────────────────────────────────────────────────────────────────────
// SucursalRepository — sp_Branches
// ─────────────────────────────────────────────────────────────────────────────
public sealed class SucursalRepository(IDapperContext db) : ISucursalRepository
{
    public async Task<Result<IEnumerable<SucursalRaw>>> ListarAsync(
        int? branchId, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm  = SpParameters.ListarSucursales(branchId);
            var rows = await conn.QueryAsync<SucursalRaw>(
                StoredProcedures.Branches, prm, commandType: CommandType.StoredProcedure);
            return Result<IEnumerable<SucursalRaw>>.Success(rows);
        }, ct);
}

// ─────────────────────────────────────────────────────────────────────────────
// ReporteRepository — sp_Reports
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ReporteRepository(IDapperContext db) : IReporteRepository
{
    public async Task<Result<IEnumerable<IngresoMensualRaw>>> IngresosAsync(
        int? year, int? month, int? branchId,
        int? userId, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm  = SpParameters.ReporteIngresos(year, month, branchId, userId);
            var rows = await conn.QueryAsync<IngresoMensualRaw>(
                StoredProcedures.Reports, prm, commandType: CommandType.StoredProcedure);
            return Result<IEnumerable<IngresoMensualRaw>>.Success(rows);
        }, ct);

    public async Task<Result<IEnumerable<AfiliadoEstadoRaw>>> AfiliadosActivosAsync(
        int? branchId, int? userId, CancellationToken ct = default) =>
        await db.ExecuteAsync(async conn =>
        {
            var prm  = SpParameters.ReporteAfiliados(branchId, userId);
            var rows = await conn.QueryAsync<AfiliadoEstadoRaw>(
                StoredProcedures.Reports, prm, commandType: CommandType.StoredProcedure);
            return Result<IEnumerable<AfiliadoEstadoRaw>>.Success(rows);
        }, ct);
}
public sealed class AuthRepository(
    IDapperContext db,
    ILogger<AuthRepository> log) : IAuthRepository
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers privados
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Traduce el ErrorId de sp_Auth (que usa HTTP codes directamente) a ResultError.
    /// </summary>
    private static ResultError MapAuthError(int errorId, string message) =>
        errorId switch
        {
            400 => new ResultError(ErrorCodes.OperacionInvalida, message, 400),
            401 => new ResultError(ErrorCodes.CredencialesInvalidas, message, 401),
            403 => new ResultError(ErrorCodes.UsuarioInactivo, message, 403),
            404 => new ResultError(ErrorCodes.UsuarioNoEncontrado, message, 404),
            409 => new ResultError(ErrorCodes.UsuarioDuplicado, message, 409),
            500 => new ResultError(ErrorCodes.ErrorInesperado, message, 500),
            _ => new ResultError(ErrorCodes.ErrorInesperado, message, 500),
        };

    /// <summary>Lee la fila de output (RS final con ErrorId/Message) del GridReader.</summary>
    private static async Task<(int? ErrorId, string Message)> ReadOutputAsync(SqlMapper.GridReader multi)
    {
        try
        {
            if (multi.IsConsumed) return (null, string.Empty);
            var row = await multi.ReadFirstOrDefaultAsync<AuthOutputRow>();
            return (row?.ErrorId, row?.Message ?? string.Empty);
        }
        catch
        {
            return (null, string.Empty);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AUTH — login
    // ─────────────────────────────────────────────────────────────────────────
    //public async Task<Result<LoginRaw>> LoginAsync(
    //    string username, string passwordHash, string? ip, string? userAgent,
    //    CancellationToken ct = default)
    //{
    //    try
    //    {
    //        //var conn = await db.GetConnectionAsync(ct);
    //        var conn = await db.ExecuteAsync(ct);

    //        var prms = AuthSpParameters.Login(username, passwordHash, ip, userAgent);

    //        using var multi = await conn.QueryMultipleAsync(
    //            StoredProcedures.Auth, prms, commandType: CommandType.StoredProcedure);

    //        // Leer primer RS como dynamic para detectar si es error o datos
    //        var firstRows = (await multi.ReadAsync<dynamic>()).ToList();
    //        var first = firstRows.FirstOrDefault() as IDictionary<string, object>;

    //        if (first is null)
    //            return Result<LoginRaw>.Failure(new ResultError(ErrorCodes.ErrorInesperado, "Sin respuesta del servidor.", 500));

    //        // ¿Es una fila de error? (tiene ErrorId y Message, no UserId)
    //        if (first.ContainsKey("ErrorId") && !first.ContainsKey("UserId"))
    //        {
    //            var eid = Convert.ToInt32(first["ErrorId"]);
    //            var msg = first.TryGetValue("Message", out var m) ? m?.ToString() ?? "" : "";
    //            return Result<LoginRaw>.Failure(MapAuthError(eid, msg));
    //        }

    //        // RS1 = datos del usuario → mapear
    //        var raw = MapToLoginRaw(first);

    //        // RS2 = output row (ignorar, ya tenemos éxito)
    //        _ = await ReadOutputAsync(multi);

    //        log.LogInformation("Login exitoso: User={Username} Id={UserId}", username, raw.UserId);
    //        return Result<LoginRaw>.Success(raw);
    //    }
    //    catch (Exception ex)
    //    {
    //        log.LogError(ex, "Error en LoginAsync para {Username}", username);
    //        return Result<LoginRaw>.Failure(new ResultError(ErrorCodes.ErrorInesperado, "Error interno al autenticar.", 500));
    //    }
    //}

    public async Task<Result<LoginRaw>> LoginAsync(string username,string passwordHash,string? ip,string? userAgent,CancellationToken ct = default)
    {
        try
        {
            return await db.ExecuteAsync(async conn =>
            {
                var prms = AuthSpParameters.Login(username,passwordHash,ip,userAgent);

                using var multi = await conn.QueryMultipleAsync(StoredProcedures.Auth,prms,commandType: CommandType.StoredProcedure);

                // RS1 = Datos del usuario o Error
                var firstRows = (await multi.ReadAsync<dynamic>()).ToList();
                var first = firstRows.FirstOrDefault() as IDictionary<string, object>;

                if (first is null)
                {
                    return Result<LoginRaw>.Failure(
                        new ResultError(ErrorCodes.ErrorInesperado,"Sin respuesta del servidor.",500)
                        );
                }

                // ¿El SP devolvió un error?
                if (first.ContainsKey("ErrorId") &&!first.ContainsKey("UserId"))
                {
                    var errorId = Convert.ToInt32(first["ErrorId"]);

                    var message = first.TryGetValue("Message", out var msg)
                        ? msg?.ToString() ?? string.Empty
                        : string.Empty;

                    return Result<LoginRaw>.Failure(
                        MapAuthError(errorId, message));
                }

                // Mapear usuario
                var raw = MapToLoginRaw(first);

                // RS2 = OUTPUT (ErrorId, Message)
                _ = await ReadOutputAsync(multi);

                log.LogInformation("Login exitoso. Username={Username}, UserId={UserId}",username,raw.UserId);

                return Result<LoginRaw>.Success(raw);

            }, ct);
        }
        catch (OperationCanceledException)
        {
            log.LogWarning( "Login cancelado para el usuario {Username}",username);

            throw;
        }
        catch (Exception ex)
        {
            log.LogError(
                ex,"Error en LoginAsync para {Username}",username);

            return Result<LoginRaw>.Failure(
                new ResultError(ErrorCodes.ErrorInesperado,"Error interno al autenticar.",500)
                );
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AUTH — refreshtoken
    // ─────────────────────────────────────────────────────────────────────────
    //public async Task<Result<RefreshTokenRaw>> RefreshTokenAsync(
    //    string refreshToken, string? ip, string? userAgent,
    //    CancellationToken ct = default)
    //{
    //    try
    //    {
    //        var conn = await db.ExecuteAsync(ct);
    //        var prms = AuthSpParameters.RefreshToken(refreshToken, ip, userAgent);

    //        using var multi = await conn.QueryMultipleAsync(
    //            StoredProcedures.Auth, prms, commandType: CommandType.StoredProcedure);

    //        var firstRows = (await multi.ReadAsync<dynamic>()).ToList();
    //        var first = firstRows.FirstOrDefault() as IDictionary<string, object>;

    //        if (first is null)
    //            return Result<RefreshTokenRaw>.Failure(new ResultError(ErrorCodes.ErrorInesperado, "Sin respuesta.", 500));

    //        if (first.ContainsKey("ErrorId") && !first.ContainsKey("UserId"))
    //        {
    //            var eid = Convert.ToInt32(first["ErrorId"]);
    //            var msg = first.TryGetValue("Message", out var m) ? m?.ToString() ?? "" : "";
    //            return Result<RefreshTokenRaw>.Failure(MapAuthError(eid, msg));
    //        }

    //        var raw = MapToRefreshTokenRaw(first);
    //        _ = await ReadOutputAsync(multi);

    //        log.LogInformation("Refresh token rotado para UserId={UserId}", raw.UserId);
    //        return Result<RefreshTokenRaw>.Success(raw);
    //    }
    //    catch (Exception ex)
    //    {
    //        log.LogError(ex, "Error en RefreshTokenAsync");
    //        return Result<RefreshTokenRaw>.Failure(new ResultError(ErrorCodes.ErrorInesperado, "Error interno al renovar token.", 500));
    //    }
    //}

    public async Task<Result<RefreshTokenRaw>> RefreshTokenAsync(string refreshToken,string? ip,string? userAgent,CancellationToken ct = default)
    {
        try
        {
            return await db.ExecuteAsync(async conn =>
            {
                var prms = AuthSpParameters.RefreshToken(refreshToken, ip, userAgent);
                using var multi = await conn.QueryMultipleAsync(StoredProcedures.Auth,prms,commandType: CommandType.StoredProcedure);
                var firstRows = (await multi.ReadAsync<dynamic>()).ToList();
                var first = firstRows.FirstOrDefault() as IDictionary<string, object>;

                if (first is null)
                {
                    return Result<RefreshTokenRaw>.Failure(
                        new ResultError(ErrorCodes.ErrorInesperado,"Sin respuesta.",500));
                }

                if (first.ContainsKey("ErrorId") && !first.ContainsKey("UserId"))
                {
                    var eid = Convert.ToInt32(first["ErrorId"]);
                    var msg = first.TryGetValue("Message", out var m)
                        ? m?.ToString() ?? string.Empty
                        : string.Empty;

                    return Result<RefreshTokenRaw>.Failure(
                        MapAuthError(eid, msg));
                }

                var raw = MapToRefreshTokenRaw(first);

                _ = await ReadOutputAsync(multi);

                log.LogInformation("Refresh token rotado para UserId={UserId}",raw.UserId);

                return Result<RefreshTokenRaw>.Success(raw);

            }, ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error en RefreshTokenAsync");

            return Result<RefreshTokenRaw>.Failure(
                new ResultError(
                    ErrorCodes.ErrorInesperado,
                    "Error interno al renovar token.",
                    500));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AUTH — logout
    // ─────────────────────────────────────────────────────────────────────────
    //public async Task<Result> LogoutAsync(string? refreshToken, int? userId, CancellationToken ct = default)
    //{
    //    try
    //    {
    //        var conn = await db.ExecuteAsync(ct);
    //        var prms = AuthSpParameters.Logout(refreshToken, userId);

    //        using var multi = await conn.QueryMultipleAsync(
    //            StoredProcedures.Auth, prms, commandType: CommandType.StoredProcedure);

    //        // RS1 = { RevokedSessions }
    //        _ = await multi.ReadAsync<dynamic>();

    //        var (eid, msg) = await ReadOutputAsync(multi);
    //        if (eid is > 0)
    //            return Result.Failure(MapAuthError(eid.Value, msg));

    //        return Result.Success();
    //    }
    //    catch (Exception ex)
    //    {
    //        log.LogError(ex, "Error en LogoutAsync");
    //        return Result.Failure(new ResultError(ErrorCodes.ErrorInesperado, "Error interno al cerrar sesión.", 500));
    //    }
    //}
    public async Task<Result> LogoutAsync(string? refreshToken,int? userId,CancellationToken ct = default)
    {
        try
        {
            return await db.ExecuteAsync(async conn =>
            {
                var prms = AuthSpParameters.Logout(refreshToken, userId);

                using var multi = await conn.QueryMultipleAsync(StoredProcedures.Auth,prms,commandType: CommandType.StoredProcedure);

                // RS1 = { RevokedSessions }
                _ = await multi.ReadAsync<dynamic>();

                var (eid, msg) = await ReadOutputAsync(multi);

                if (eid is > 0)
                {
                    return Result.Failure(MapAuthError(eid.Value, msg));
                }

                log.LogInformation("Logout exitoso. UserId={UserId}, RefreshToken={RefreshToken}",userId,refreshToken);
                return Result.Success();

            }, ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex,"Error en LogoutAsync. UserId={UserId}",userId);

            return Result.Failure(
                new ResultError(ErrorCodes.ErrorInesperado,"Error interno al cerrar sesión.",500)
                );
        }
    }
    // ─────────────────────────────────────────────────────────────────────────
    // AUTH — revokeaccesstoken
    // ─────────────────────────────────────────────────────────────────────────
    //public async Task<Result> RevokeAccessTokenAsync(
    //    string jti, DateTime accessTokenExp, int? userId, string? reason,
    //    CancellationToken ct = default)
    //{
    //    try
    //    {
    //        var conn = await db.ExecuteAsync(ct);
    //        var prms = AuthSpParameters.RevokeAccessToken(jti, accessTokenExp, userId, reason);

    //        using var multi = await conn.QueryMultipleAsync(
    //            StoredProcedures.Auth, prms, commandType: CommandType.StoredProcedure);

    //        _ = await multi.ReadAsync<dynamic>();   // RS1 = { AlreadyRevoked } o { Inserted }
    //        var (eid, msg) = await ReadOutputAsync(multi);

    //        if (eid is > 0)
    //            return Result.Failure(MapAuthError(eid.Value, msg));

    //        return Result.Success();
    //    }
    //    catch (Exception ex)
    //    {
    //        log.LogError(ex, "Error en RevokeAccessTokenAsync jti={Jti}", jti);
    //        return Result.Failure(new ResultError(ErrorCodes.ErrorInesperado, "Error al revocar token.", 500));
    //    }
    //}
    public async Task<Result> RevokeAccessTokenAsync(string jti,DateTime accessTokenExp,int? userId,string? reason,CancellationToken ct = default)
    {
        try
        {
            return await db.ExecuteAsync(async conn =>
            {
                var prms = AuthSpParameters.RevokeAccessToken(jti,accessTokenExp,userId,reason);

                using var multi = await conn.QueryMultipleAsync(StoredProcedures.Auth,prms,commandType: CommandType.StoredProcedure);

                // RS1 = { AlreadyRevoked } o { Inserted }
                _ = await multi.ReadAsync<dynamic>();

                // RS2 = OUTPUT
                var (errorId, message) = await ReadOutputAsync(multi);

                if (errorId is > 0)
                {
                    return Result.Failure(
                        MapAuthError(errorId.Value, message));
                }

                log.LogInformation("AccessToken revocado correctamente. Jti={Jti}, UserId={UserId}",jti,userId);

                return Result.Success();

            }, ct);
        }
        catch (OperationCanceledException)
        {
            log.LogWarning("Revocación del AccessToken cancelada. Jti={Jti}",jti);

            throw;
        }
        catch (Exception ex)
        {
            log.LogError(ex,"Error en RevokeAccessTokenAsync. Jti={Jti}, UserId={UserId}",jti,userId);

            return Result.Failure(
                new ResultError(ErrorCodes.ErrorInesperado,"Error al revocar el token.",500)
                );
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AUTH — validateblacklist (llamado desde middleware — debe ser MUY rápido)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<Result<bool>> IsTokenRevokedAsync(
    string jti,
    CancellationToken ct = default)
    {
        try
        {
            return await db.ExecuteAsync(async conn =>
            {
                var prms = AuthSpParameters.ValidateBlacklist(jti);

                using var multi = await conn.QueryMultipleAsync(
                    StoredProcedures.Auth,
                    prms,
                    commandType: CommandType.StoredProcedure);

                var row = await multi.ReadFirstOrDefaultAsync<dynamic>();

                bool isRevoked = row is not null &&
                                 Convert.ToBoolean(((IDictionary<string, object>)row)["IsRevoked"]);

                return Result<bool>.Success(isRevoked);

            }, ct);
        }
        catch (OperationCanceledException)
        {
            log.LogWarning("Validación de blacklist cancelada. Jti={Jti}", jti);
            throw;
        }
        catch (Exception ex)
        {
            log.LogError(ex,
                "Error en IsTokenRevokedAsync Jti={Jti}", jti);

            // Fail Open
            return Result<bool>.Success(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // USERS — createuser
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<Result<UsuarioSistemaRaw>> CrearUsuarioAsync(
    CrearUsuarioParams p,
    CancellationToken ct = default)
    {
        try
        {
            return await db.ExecuteAsync(async conn =>
            {
                var prms = AuthSpParameters.CrearUsuario(p);

                using var multi = await conn.QueryMultipleAsync(
                    StoredProcedures.Auth,
                    prms,
                    commandType: CommandType.StoredProcedure);

                var firstRows = (await multi.ReadAsync<dynamic>()).ToList();

                var first = firstRows.FirstOrDefault() as IDictionary<string, object>;

                if (first is null)
                {
                    return Result<UsuarioSistemaRaw>.Failure(
                        new ResultError(
                            ErrorCodes.ErrorInesperado,
                            "Sin respuesta.",
                            500));
                }

                if (first.ContainsKey("ErrorId") &&
                    !first.ContainsKey("UserId"))
                {
                    var eid = Convert.ToInt32(first["ErrorId"]);

                    var msg = first.TryGetValue("Message", out var m)
                        ? m?.ToString() ?? string.Empty
                        : string.Empty;

                    return Result<UsuarioSistemaRaw>.Failure(
                        MapAuthError(eid, msg));
                }

                var raw = MapToUsuarioSistemaRaw(first);

                _ = await ReadOutputAsync(multi);

                log.LogInformation(
                    "Usuario creado: Id={UserId} Username={Username}",
                    raw.UserId,
                    raw.Username);

                return Result<UsuarioSistemaRaw>.Success(raw);

            }, ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex,
                "Error en CrearUsuarioAsync Username={Username}",
                p.Username);

            return Result<UsuarioSistemaRaw>.Failure(
                new ResultError(
                    ErrorCodes.ErrorInesperado,
                    "Error al crear usuario.",
                    500));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // USERS — deactivateuser
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<Result> DarDeBajaAsync(
     int userId,
     string reason,
     int operatedBy,
     CancellationToken ct = default)
    {
        try
        {
            return await db.ExecuteAsync(async conn =>
            {
                var prms = AuthSpParameters.DarDeBaja(
                    userId,
                    reason,
                    operatedBy);

                using var multi = await conn.QueryMultipleAsync(
                    StoredProcedures.Auth,
                    prms,
                    commandType: CommandType.StoredProcedure);

                var firstRows = (await multi.ReadAsync<dynamic>()).ToList();

                var first = firstRows.FirstOrDefault() as IDictionary<string, object>;

                if (first is not null &&
                    first.ContainsKey("ErrorId") &&
                    !first.ContainsKey("UserId"))
                {
                    var eid = Convert.ToInt32(first["ErrorId"]);

                    var msg = first.TryGetValue("Message", out var m)
                        ? m?.ToString() ?? string.Empty
                        : string.Empty;

                    return Result.Failure(
                        MapAuthError(eid, msg));
                }

                _ = await ReadOutputAsync(multi);

                log.LogWarning(
                    "Usuario dado de baja: Id={UserId} OperadoPor={OperatedBy}",
                    userId,
                    operatedBy);

                return Result.Success();

            }, ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex,
                "Error en DarDeBajaAsync UserId={UserId}",
                userId);

            return Result.Failure(
                new ResultError(
                    ErrorCodes.ErrorInesperado,
                    "Error al dar de baja el usuario.",
                    500));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // USERS — getuser
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<Result<UsuarioSistemaRaw?>> ObtenerUsuarioAsync(
    int? userId,
    string? username,
    CancellationToken ct = default)
    {
        try
        {
            return await db.ExecuteAsync(async conn =>
            {
                var prms = AuthSpParameters.ObtenerUsuario(
                    userId,
                    username);

                using var multi = await conn.QueryMultipleAsync(
                    StoredProcedures.Auth,
                    prms,
                    commandType: CommandType.StoredProcedure);

                var firstRows = (await multi.ReadAsync<dynamic>()).ToList();

                var first = firstRows.FirstOrDefault() as IDictionary<string, object>;

                if (first is null)
                    return Result<UsuarioSistemaRaw?>.Success(null);

                if (first.ContainsKey("ErrorId") &&
                    !first.ContainsKey("UserId"))
                {
                    var eid = Convert.ToInt32(first["ErrorId"]);

                    var msg = first.TryGetValue("Message", out var m)
                        ? m?.ToString() ?? string.Empty
                        : string.Empty;

                    return Result<UsuarioSistemaRaw?>.Failure(
                        MapAuthError(eid, msg));
                }

                return Result<UsuarioSistemaRaw?>.Success(
                    MapToUsuarioSistemaRaw(first));

            }, ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error en ObtenerUsuarioAsync");

            return Result<UsuarioSistemaRaw?>.Failure(
                new ResultError(
                    ErrorCodes.ErrorInesperado,
                    "Error al obtener usuario.",
                    500));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // USERS — listusers
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<Result<(IEnumerable<UsuarioSistemaListaRaw> Items, int Total)>> ListarUsuariosAsync(
    int? roleId,
    int? branchId,
    int pageNumber,
    int pageSize,
    CancellationToken ct = default)
    {
        try
        {
            return await db.ExecuteAsync(async conn =>
            {
                var prms = AuthSpParameters.ListarUsuarios(
                    roleId,
                    branchId,
                    pageNumber,
                    pageSize);

                using var multi = await conn.QueryMultipleAsync(
                    StoredProcedures.Auth,
                    prms,
                    commandType: CommandType.StoredProcedure);

                var rows = (await multi.ReadAsync<UsuarioSistemaListaRaw>()).ToList();

                _ = await ReadOutputAsync(multi);

                int total = rows.FirstOrDefault()?.TotalRecords ?? 0;

                return Result<(IEnumerable<UsuarioSistemaListaRaw>, int)>
                    .Success((rows, total));

            }, ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error en ListarUsuariosAsync");

            return Result<(IEnumerable<UsuarioSistemaListaRaw>, int)>
                .Failure(
                    new ResultError(
                        ErrorCodes.ErrorInesperado,
                        "Error al listar usuarios.",
                        500));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MAINTENANCE — purgetokens
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<Result<PurgaTokensRaw>> PurgarTokensAsync(
    CancellationToken ct = default)
    {
        try
        {
            return await db.ExecuteAsync(async conn =>
            {
                var prms = AuthSpParameters.PurgarTokens();

                using var multi = await conn.QueryMultipleAsync(
                    StoredProcedures.Auth,
                    prms,
                    commandType: CommandType.StoredProcedure);

                var row = await multi.ReadFirstOrDefaultAsync<PurgaTokensRaw>();

                _ = await ReadOutputAsync(multi);

                log.LogInformation(
                    "Purga ejecutada: Refresh={Refresh} Access={Access}",
                    row?.PurgedRefreshTokens,
                    row?.PurgedAccessTokens);

                return row is not null
                    ? Result<PurgaTokensRaw>.Success(row)
                    : Result<PurgaTokensRaw>.Failure(
                        new ResultError(
                            ErrorCodes.ErrorInesperado,
                            "Sin respuesta de purga.",
                            500));

            }, ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error en PurgarTokensAsync");

            return Result<PurgaTokensRaw>.Failure(
                new ResultError(
                    ErrorCodes.ErrorInesperado,
                    "Error en purga de tokens.",
                    500));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mappers privados (dynamic → typed)
    // ─────────────────────────────────────────────────────────────────────────
    private static T Get<T>(IDictionary<string, object> d, string key, T defaultVal = default!)
    {
        if (!d.TryGetValue(key, out var val) || val is null) return defaultVal;
        if (val is T t) return t;
        return (T)Convert.ChangeType(val, typeof(T));
    }

    private static LoginRaw MapToLoginRaw(IDictionary<string, object> d) => new()
    {
        UserId = Get<int>(d, "UserId"),
        Username = Get<string>(d, "Username"),
        FullName = Get<string>(d, "FullName"),
        Email = Get<string>(d, "Email"),
        RoleId = Get<int>(d, "RoleId"),
        RoleCode = Get<string>(d, "RoleCode"),
        RoleName = Get<string>(d, "RoleName"),
        BranchId = d.TryGetValue("BranchId", out var bid) && bid is not null ? (int?)Convert.ToInt32(bid) : null,
    };

    private static RefreshTokenRaw MapToRefreshTokenRaw(IDictionary<string, object> d) => new()
    {
        UserId = Get<int>(d, "UserId"),
        Username = Get<string>(d, "Username"),
        FullName = Get<string>(d, "FullName"),
        Email = Get<string>(d, "Email"),
        RoleId = Get<int>(d, "RoleId"),
        RoleCode = Get<string>(d, "RoleCode"),
        RoleName = Get<string>(d, "RoleName"),
        BranchId = d.TryGetValue("BranchId", out var bid) && bid is not null ? (int?)Convert.ToInt32(bid) : null,
        NewRefreshToken = Get<string>(d, "NewRefreshToken"),
        RefreshTokenExpiry = Get<DateTime>(d, "RefreshTokenExpiry"),
    };

    private static UsuarioSistemaRaw MapToUsuarioSistemaRaw(IDictionary<string, object> d) => new()
    {
        UserId = Get<int>(d, "UserId"),
        Username = Get<string>(d, "Username"),
        FullName = Get<string>(d, "FullName"),
        Email = Get<string>(d, "Email"),
        RoleId = Get<int>(d, "RoleId"),
        RoleCode = Get<string>(d, "RoleCode"),
        RoleName = Get<string>(d, "RoleName"),
        BranchId = d.TryGetValue("BranchId", out var bid) && bid is not null ? (int?)Convert.ToInt32(bid) : null,
        BranchName = d.TryGetValue("BranchName", out var bn) ? bn?.ToString() : null,
        IsActive = Get<bool>(d, "IsActive"),
        LastLogin = d.TryGetValue("LastLogin", out var ll) && ll is not null ? (DateTime?)Convert.ToDateTime(ll) : null,
        CreatedAt = Get<DateTime>(d, "CreatedAt"),
        UpdatedAt = d.TryGetValue("UpdatedAt", out var ua) && ua is not null ? (DateTime?)Convert.ToDateTime(ua) : null,
        DeactivatedAt = d.TryGetValue("DeactivatedAt", out var da) && da is not null ? (DateTime?)Convert.ToDateTime(da) : null,
        DeactivationReason = d.TryGetValue("DeactivationReason", out var dr) ? dr?.ToString() : null,
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// DTO interno para leer el RS de output de sp_Auth
// ─────────────────────────────────────────────────────────────────────────────
file class AuthOutputRow
{
    public int? ErrorId { get; set; }
    public string? Message { get; set; }
}
