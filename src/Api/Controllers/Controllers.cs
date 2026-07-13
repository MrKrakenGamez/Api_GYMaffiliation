using GymAffiliate.Application.DTOs.Requests;
using GymAffiliate.Application.UseCases.Acceso;
using GymAffiliate.Application.UseCases.Afiliados;
using GymAffiliate.Application.UseCases.Membresias;
using GymAffiliate.Application.UseCases.Notificaciones;
using GymAffiliate.Application.UseCases.Pagos;
using GymAffiliate.Application.UseCases.Reportes;
using GymAffiliate.Shared.Result;
using Microsoft.AspNetCore.Mvc;

namespace GymAffiliate.Api.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
// GymBaseController — helpers shared across all controllers
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
public abstract class GymBaseController : ControllerBase
{
    /// <summary>Converts a Result into the correct IActionResult (200/4xx/5xx).</summary>
    protected IActionResult ToAction<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse<T>.Ok(result.Value!));

        var err = result.Error!;
        var body = ApiResponse<T>.Fail(err);
        return StatusCode(err.HttpStatus, body);
    }

    protected IActionResult ToAction(GymAffiliate.Shared.Result.Result result)
    {
        if (result.IsSuccess) return Ok(ApiResponse.Ok());
        var err = result.Error!;
        return StatusCode(err.HttpStatus, ApiResponse.Fail(err));
    }

    protected IActionResult ToPagedAction<T>(
        GymAffiliate.Shared.Result.Result<(IEnumerable<T> Items, int Total)> result,
        int page, int pageSize)
    {
        if (result.IsFailure)
        {
            var err = result.Error!;
            return StatusCode(err.HttpStatus, ApiResponse<object>.Fail(err));
        }
        var (items, total) = result.Value;
        return Ok(PagedApiResponse<T>.Ok(items, total, page, pageSize));
    }

    /// <summary>Gets the current user ID from JWT claims (returns null if not authenticated).</summary>
    protected int? CurrentUserId =>
        User.Claims.FirstOrDefault(c => c.Type == "userId") is { } claim
        && int.TryParse(claim.Value, out var id) ? id : null;

    protected string? ClientIp =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    protected string? SessionId =>
        HttpContext.TraceIdentifier;
}

// ─────────────────────────────────────────────────────────────────────────────
// AfiliadosController
// ─────────────────────────────────────────────────────────────────────────────
[Route("api/afiliados")]
public sealed class AfiliadosController(
    CrearAfiliadoHandler    crearHandler,
    ActualizarAfiliadoHandler actualizarHandler,
    EliminarAfiliadoHandler eliminarHandler,
    ObtenerAfiliadoHandler  obtenerHandler,
    ListarAfiliadosHandler  listarHandler) : GymBaseController
{
    // POST /api/afiliados
    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromBody] CrearAfiliadoRequest request,
        CancellationToken ct)
    {
        var result = await crearHandler.HandleAsync(request, CurrentUserId, ClientIp, SessionId, ct);
        //var result = await crearHandler.HandleAsync(request, 1, ClientIp, SessionId, ct);

        return ToAction(result);
    }

    // PUT /api/afiliados/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(
        int id,
        [FromBody] ActualizarAfiliadoRequest request,
        CancellationToken ct)
    {
        var result = await actualizarHandler.HandleAsync(id, request, CurrentUserId, ClientIp, SessionId, ct);
        //var result = await actualizarHandler.HandleAsync(id, request, 1, ClientIp, SessionId, ct);

        return ToAction(result);
    }

    // DELETE /api/afiliados/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(
        int id,
        [FromQuery] string? notes,
        CancellationToken ct)
    {
        var result = await eliminarHandler.HandleAsync(id, notes, CurrentUserId, ClientIp, SessionId, ct);
        //var result = await eliminarHandler.HandleAsync(id, notes, 1, ClientIp, SessionId, ct);

        return ToAction(result);
    }

    // GET /api/afiliados/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerPorId(int id, CancellationToken ct)
    {
        var result = await obtenerHandler.HandleAsync(id, null, null, CurrentUserId, ct);

        //var result = await obtenerHandler.HandleAsync(id, null, null, 1, ct);
        return ToAction(result);
    }

    // GET /api/afiliados?filterSearch=...&pageNumber=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] ListarAfiliadosRequest request,
        CancellationToken ct)
    {
        var result = await listarHandler.HandleAsync(request, CurrentUserId, ct);

        //var result = await listarHandler.HandleAsync(request, 1, ct);
        return ToPagedAction(result, request.PageNumber, request.PageSize);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// MembresiasController
// ─────────────────────────────────────────────────────────────────────────────
[Route("api/membresias")]
public sealed class MembresiasController(
    AsignarMembresiaHandler asignarHandler,
    RenovarMembresiaHandler renovarHandler,
    CambiarPlanHandler      cambiarHandler) : GymBaseController
{
    // POST /api/membresias/asignar
    [HttpPost("asignar")]
    public async Task<IActionResult> Asignar(
        [FromBody] AsignarMembresiaRequest request,
        CancellationToken ct)
    {
        var result = await asignarHandler.HandleAsync(request, CurrentUserId, ClientIp, SessionId, ct);

        //var result = await asignarHandler.HandleAsync(request, 1, ClientIp, SessionId, ct);
        return ToAction(result);
    }

    // POST /api/membresias/renovar
    [HttpPost("renovar")]
    public async Task<IActionResult> Renovar(
        [FromBody] RenovarMembresiaRequest request,
        CancellationToken ct)
    {
        var result = await renovarHandler.HandleAsync(request, CurrentUserId, ClientIp, SessionId, ct);

        //var result = await renovarHandler.HandleAsync(request, 1, ClientIp, SessionId, ct);
        return ToAction(result);
    }

    // PUT /api/membresias/cambiar-plan
    [HttpPut("cambiar-plan")]
    public async Task<IActionResult> CambiarPlan(
        [FromBody] CambiarPlanRequest request,
        CancellationToken ct)
    {
        var result = await cambiarHandler.HandleAsync(request, CurrentUserId, ClientIp, SessionId, ct);

        //var result = await cambiarHandler.HandleAsync(request, 1, ClientIp, SessionId, ct);
        return ToAction(result);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PagosController
// ─────────────────────────────────────────────────────────────────────────────
[Route("api/pagos")]
public sealed class PagosController(
    RegistrarPagoHandler  registrarHandler,
    HistorialPagosHandler historialHandler) : GymBaseController
{
    // POST /api/pagos/registrar
    [HttpPost("registrar")]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarPagoRequest request,
        CancellationToken ct)
    {
        var result = await registrarHandler.HandleAsync(request, CurrentUserId, ClientIp, SessionId, ct);

        //var result = await registrarHandler.HandleAsync(request, 1, ClientIp, SessionId, ct);
        return ToAction(result);
    }

    // GET /api/pagos/historial/{afiliadoId}
    [HttpGet("historial/{afiliadoId:int}")]
    public async Task<IActionResult> Historial(
        int afiliadoId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? branchId,
        CancellationToken ct)
    {
        var result = await historialHandler.HandleAsync(
            new ListarPagosRequest(afiliadoId, from, to, branchId),
            CurrentUserId, ct);
        //var result = await historialHandler.HandleAsync(new ListarPagosRequest(afiliadoId, from, to, branchId),1, ct);
        return ToAction(result);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// AccesoController
// ─────────────────────────────────────────────────────────────────────────────
[Route("api/acceso")]
public sealed class AccesoController(
    RegistrarIngresoHandler registrarHandler,
    ValidarAccesoHandler    validarHandler) : GymBaseController
{
    // GET /api/acceso/validar?affiliateId=1&branchId=1
    [HttpGet("validar")]
    public async Task<IActionResult> Validar(
        [FromQuery] int affiliateId,
        CancellationToken ct)
    {
        var result = await validarHandler.HandleAsync(affiliateId, ct);
        return ToAction(result);
    }

    // POST /api/acceso/registrar-ingreso
    [HttpPost("registrar-ingreso")]
    public async Task<IActionResult> RegistrarIngreso(
        [FromBody] RegistrarIngresoRequest request,
        CancellationToken ct)
    {
        var result = await registrarHandler.HandleAsync(request, CurrentUserId, ClientIp, SessionId, ct);

        //var result = await registrarHandler.HandleAsync(request, 1, ClientIp, SessionId, ct);
        return ToAction(result);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// NotificacionesController
// ─────────────────────────────────────────────────────────────────────────────
[Route("api/notificaciones")]
public sealed class NotificacionesController(
    VencimientosHandler  vencimientosHandler,
    EnviarAlertaHandler  alertaHandler) : GymBaseController
{
    // GET /api/notificaciones/por-vencer?daysAhead=3
    [HttpGet("por-vencer")]
    public async Task<IActionResult> PorVencer(
        [FromQuery] int daysAhead = 3,
        CancellationToken ct = default)
    {
        var result = await vencimientosHandler.HandleAsync(daysAhead, CurrentUserId, ct);

        //var result = await vencimientosHandler.HandleAsync(daysAhead, 1, ct);
        return ToAction(result);
    }

    // POST /api/notificaciones/enviar-alerta
    [HttpPost("enviar-alerta")]
    public async Task<IActionResult> EnviarAlerta(
        [FromBody] EnviarAlertaRequest request,
        CancellationToken ct)
    {
        var result = await alertaHandler.HandleAsync(request, CurrentUserId, SessionId, ct);

        //var result = await alertaHandler.HandleAsync(request, 1, SessionId, ct);
        return ToAction(result);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ReportesController
// ─────────────────────────────────────────────────────────────────────────────
[Route("api/reportes")]
public sealed class ReportesController(
    ReporteIngresosHandler  ingresosHandler,
    AfiliadosActivosHandler activosHandler) : GymBaseController
{
    // GET /api/reportes/ingresos?year=2025&month=4&branchId=1
    [HttpGet("ingresos")]
    public async Task<IActionResult> Ingresos(
        [FromQuery] ReporteIngresosRequest request,
        CancellationToken ct)
    {
        var result = await ingresosHandler.HandleAsync(request, CurrentUserId, ct);

        //var result = await ingresosHandler.HandleAsync(request, 1, ct);
        return ToAction(result);
    }

    // GET /api/reportes/afiliados-activos?branchId=1
    [HttpGet("afiliados-activos")]
    public async Task<IActionResult> AfiliadosActivos(
        [FromQuery] int? branchId,
        CancellationToken ct)
    {
        var result = await activosHandler.HandleAsync(branchId, CurrentUserId, ct);

        //var result = await activosHandler.HandleAsync(branchId, 1, ct);
        return ToAction(result);
    }
}
