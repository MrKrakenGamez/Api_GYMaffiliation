using Dapper;
using GymAffiliate.Domain.Interfaces.Repositories;

namespace GymAffiliate.Infrastructure.Persistence.Dapper.Parameters;

// ─────────────────────────────────────────────────────────────────────────────
// SpOutputRow
// Todos los SPs retornan al final un SELECT con la estructura:
//   SELECT OperationType, [OutputId], ErrorId, ErrorMessage, OperationDate
// Dapper mapea ese último RS a este record.
// ErrorId = null o 0 → éxito.  ErrorId > 0 → error de negocio.
// ─────────────────────────────────────────────────────────────────────────────

public class SpOutputRow
{
    public string? OperationType { get; set; }
    public int? AffiliateId { get; set; }  // ✅ LO QUE FALTABA
    public int? ErrorId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? OperationDate { get; set; } // ✅ recomendado nullable
}


public record SpResult(int? ErrorId, string? ErrorMessage)
{
    public bool IsSuccess => ErrorId is null or 0;
    public bool IsError   => !IsSuccess;
}

// ─────────────────────────────────────────────────────────────────────────────
// SpParameters — construye los DynamicParameters de entrada para cada SP.
// NO hay parámetros OUTPUT de ADO.NET: ErrorId y ErrorMessage vienen
// en el último result set SELECT del SP.
// ─────────────────────────────────────────────────────────────────────────────
public static class SpParameters
{
    private static void AddAudit(DynamicParameters p, int? userId, string? ip, string? session)
    {
        p.Add("@ExecutedByUserId", userId);
        p.Add("@IpAddress",        ip);
        p.Add("@SessionId",        session);
    }

    // ── sp_Affiliates ─────────────────────────────────────────────────────────

    public static DynamicParameters Crear(CrearAfiliadoParams r)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",    "addaffiliated");
        p.Add("@DocumentNumber",   r.DocumentNumber);
        p.Add("@DocumentType",     r.DocumentType);
        p.Add("@FirstName",        r.FirstName);
        p.Add("@LastName",         r.LastName);
        p.Add("@BirthDate",        r.BirthDate.ToDateTime(TimeOnly.MinValue));
        p.Add("@NewEmail",         r.Email);
        p.Add("@Phone",            r.Phone);
        p.Add("@Address",          r.Address);
        p.Add("@EmergencyContact", r.EmergencyContact);
        p.Add("@EmergencyPhone",   r.EmergencyPhone);
        p.Add("@BaseBranchId",     r.BaseBranchId);
        p.Add("@Notes",            r.Notes ?? "");
        AddAudit(p, r.UserId, r.Ip, r.Session);
        return p;
    }

    public static DynamicParameters Actualizar(ActualizarAfiliadoParams r)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",    "updateaffiliated");
        p.Add("@AffiliateId",      r.AffiliateId);
        p.Add("@FirstName",        r.FirstName);
        p.Add("@LastName",         r.LastName);
        p.Add("@BirthDate",        r.BirthDate?.ToDateTime(TimeOnly.MinValue));
        p.Add("@NewEmail",         r.NewEmail);
        p.Add("@Phone",            r.Phone);
        p.Add("@Address",          r.Address);
        p.Add("@EmergencyContact", r.EmergencyContact);
        p.Add("@EmergencyPhone",   r.EmergencyPhone);
        p.Add("@BaseBranchId",     r.BaseBranchId);
        p.Add("@Notes",            r.Notes ?? "");
        AddAudit(p, r.UserId, r.Ip, r.Session);
        return p;
    }

    public static DynamicParameters Eliminar(int id, string? notes, int? userId, string? ip, string? session)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType", "removeaffiliated");
        p.Add("@AffiliateId",   id);
        p.Add("@Notes",         notes ?? "");
        AddAudit(p, userId, ip, session);
        return p;
    }

    public static DynamicParameters Ver(int? id, string? doc, string? email, int? userId)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",    "viewaffiliated");
        p.Add("@AffiliateId",      id ?? 0);
        p.Add("@DocumentNumber",   doc ?? "");
        p.Add("@Email",            email ?? "");
        p.Add("@ExecutedByUserId", userId);
        return p;
    }

    public static DynamicParameters Listar(ListarAfiliadosParams r)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",    "listaffiliated");
        p.Add("@FilterStatus",     r.FilterStatus);
        p.Add("@FilterBranchId",   r.FilterBranchId);
        p.Add("@FilterSearch",     r.FilterSearch ?? "");
        p.Add("@PageNumber",       r.PageNumber);
        p.Add("@PageSize",         r.PageSize);
        p.Add("@ExecutedByUserId", r.UserId);
        return p;
    }

    // ── sp_Memberships ────────────────────────────────────────────────────────

    public static DynamicParameters Asignar(AsignarMembresiaParams r)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",    "addmembership");
        p.Add("@AffiliateId",      r.AffiliateId);
        p.Add("@MembershipTypeId", r.MembershipTypeId);
        p.Add("@BranchId",         r.BranchId);
        p.Add("@StartDate",        r.StartDate?.ToDateTime(TimeOnly.MinValue));
        p.Add("@Notes",            r.Notes ?? "");
        AddAudit(p, r.UserId, r.Ip, r.Session);
        return p;
    }

    public static DynamicParameters Renovar(RenovarMembresiaParams r)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",    "renewmembership");
        p.Add("@AffiliateId",      r.AffiliateId);
        p.Add("@MembershipTypeId", r.MembershipTypeId);
        p.Add("@BranchId",         r.BranchId);
        p.Add("@Notes",            r.Notes ?? "");
        AddAudit(p, r.UserId, r.Ip, r.Session);
        return p;
    }

    public static DynamicParameters CambiarPlan(CambiarPlanParams r)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",    "changemembership");
        p.Add("@AffiliateId",      r.AffiliateId);
        p.Add("@MembershipTypeId", r.NewMembershipTypeId);
        p.Add("@BranchId",         r.BranchId);
        p.Add("@StartDate",        r.StartDate?.ToDateTime(TimeOnly.MinValue));
        AddAudit(p, r.UserId, r.Ip, r.Session);
        return p;
    }

    public static DynamicParameters Cancelar(int? memId, int? affId, string? notes, int? userId)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",    "cancelmembership");
        p.Add("@MembershipId",     memId);
        p.Add("@AffiliateId",      affId);
        p.Add("@Notes",            notes ?? "");
        p.Add("@ExecutedByUserId", userId);
        return p;
    }

    public static DynamicParameters Validar(int affiliateId)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType", "validateactive");
        p.Add("@AffiliateId",   affiliateId);
        return p;
    }

    // ── sp_Payments ───────────────────────────────────────────────────────────

    public static DynamicParameters RegistrarPago(RegistrarPagoParams r)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",   "addpayment");
        p.Add("@AffiliateId",     r.AffiliateId);
        p.Add("@MembershipId",    r.MembershipId);
        p.Add("@PaymentMethodId", r.PaymentMethodId);
        p.Add("@Amount",          r.Amount);
        p.Add("@ReferenceNumber", r.ReferenceNumber);
        p.Add("@Notes",           r.Notes ?? "");
        AddAudit(p, r.UserId, r.Ip, r.Session);
        return p;
    }

    public static DynamicParameters CancelarPago(int id, string? notes, int? userId, string? ip, string? session)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType", "cancelpayment");
        p.Add("@PaymentId",     id);
        p.Add("@Notes",         notes ?? "");
        AddAudit(p, userId, ip, session);
        return p;
    }

    public static DynamicParameters ListarPagos(int? affId, DateOnly? from, DateOnly? to, int? branchId, int? userId)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",    "listpayments");
        p.Add("@AffiliateId",      affId);
        p.Add("@FilterDateFrom",   from?.ToDateTime(TimeOnly.MinValue));
        p.Add("@FilterDateTo",     to?.ToDateTime(TimeOnly.MinValue));
        p.Add("@FilterBranchId",   branchId);
        p.Add("@ExecutedByUserId", userId);
        return p;
    }

    // ── sp_CheckIn ────────────────────────────────────────────────────────────

    public static DynamicParameters RegistrarCheckIn(int affId, int branchId, int? userId, string? ip, string? session)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType", "registercheckin");
        p.Add("@AffiliateId",   affId);
        p.Add("@BranchId",      branchId);
        AddAudit(p, userId, ip, session);
        return p;
    }

    // ── sp_Notifications ──────────────────────────────────────────────────────

    public static DynamicParameters GenerarAlertas(int daysAhead, string channel, int? userId, string? session)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",    "generateexpirynotifications");
        p.Add("@DaysAhead",        daysAhead);
        p.Add("@Channel",          channel);
        p.Add("@ExecutedByUserId", userId);
        p.Add("@SessionId",        session);
        return p;
    }

    public static DynamicParameters ListarPendientes(int? affId)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType", "listpending");
        p.Add("@AffiliateId",   affId);
        return p;
    }

    public static DynamicParameters MarcarEnviada(int notifId, string? errorDetail, int? userId)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",    "markassent");
        p.Add("@NotificationId",   notifId);
        p.Add("@ErrorDetail",      errorDetail);
        p.Add("@ExecutedByUserId", userId);
        return p;
    }

    // ── sp_Branches ───────────────────────────────────────────────────────────

    public static DynamicParameters ListarSucursales(int? branchId)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType", branchId.HasValue ? "viewbranch" : "listbranches");
        p.Add("@BranchId",      branchId);
        return p;
    }

    // ── sp_Reports ────────────────────────────────────────────────────────────

    public static DynamicParameters ReporteIngresos(int? year, int? month, int? branchId, int? userId)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",    "monthlyrevenue");
        p.Add("@Year",             year);
        p.Add("@Month",            month);
        p.Add("@BranchId",         branchId);
        p.Add("@ExecutedByUserId", userId);
        return p;
    }

    public static DynamicParameters ReporteAfiliados(int? branchId, int? userId)
    {
        var p = new DynamicParameters();
        p.Add("@OperationType",    "affiliatestatus");
        p.Add("@BranchId",         branchId);
        p.Add("@ExecutedByUserId", userId);
        return p;
    }
}
