namespace GymAffiliate.Application.DTOs.Requests;

// ─────────────────────────────────────────────────────────────────────────────
// Afiliados
// ─────────────────────────────────────────────────────────────────────────────

public record CrearAfiliadoRequest(
    string DocumentNumber,
    string DocumentType,
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    string Email,
    string? Phone,
    string? Address,
    string? EmergencyContact,
    string? EmergencyPhone,
    int? BaseBranchId,
    string? Notes);

public record ActualizarAfiliadoRequest(
    string? FirstName,
    string? LastName,
    DateOnly? BirthDate,
    string? Email,
    string? Phone,
    string? Address,
    string? EmergencyContact,
    string? EmergencyPhone,
    int? BaseBranchId,
    string? Notes);

public record ListarAfiliadosRequest(
    int? FilterStatus,
    int? FilterBranchId,
    string? FilterSearch,
    int PageNumber = 1,
    int PageSize   = 20);

// ─────────────────────────────────────────────────────────────────────────────
// Membresías
// ─────────────────────────────────────────────────────────────────────────────

public record AsignarMembresiaRequest(
    int AffiliateId,
    int MembershipTypeId,
    int? BranchId,
    DateOnly? StartDate,
    string? Notes);

public record RenovarMembresiaRequest(
    int AffiliateId,
    int? MembershipTypeId,
    int? BranchId,
    string? Notes);

public record CambiarPlanRequest(
    int AffiliateId,
    int NewMembershipTypeId,
    int? BranchId,
    DateOnly? StartDate);

// ─────────────────────────────────────────────────────────────────────────────
// Pagos
// ─────────────────────────────────────────────────────────────────────────────

public record RegistrarPagoRequest(
    int AffiliateId,
    int MembershipId,
    int PaymentMethodId,
    decimal Amount,
    string? ReferenceNumber,
    string? Notes);

public record ListarPagosRequest(
    int? AffiliateId,
    DateOnly? From,
    DateOnly? To,
    int? BranchId);

// ─────────────────────────────────────────────────────────────────────────────
// Acceso / CheckIn
// ─────────────────────────────────────────────────────────────────────────────

public record RegistrarIngresoRequest(
    int AffiliateId,
    int BranchId);

public record ValidarAccesoRequest(
    int AffiliateId,
    int BranchId);

// ─────────────────────────────────────────────────────────────────────────────
// Notificaciones
// ─────────────────────────────────────────────────────────────────────────────

public record EnviarAlertaRequest(
    int DaysAhead  = 3,
    string Channel = "EMAIL");

// ─────────────────────────────────────────────────────────────────────────────
// Reportes
// ─────────────────────────────────────────────────────────────────────────────

public record ReporteIngresosRequest(
    int? Year,
    int? Month,
    int? BranchId);

// ─────────────────────────────────────────────────────────────────────────────
// Auth
// ─────────────────────────────────────────────────────────────────────────────

public record LoginRequest(
    string Username,
    string Password);         // contraseña en texto plano; el handler la hashea

public record RefreshTokenRequest(
    string RefreshToken);

public record LogoutRequest(
    string? RefreshToken,     // logout de la sesión actual
    bool LogoutAll = false); // true → cierra TODAS las sesiones del usuario

public record CrearUsuarioRequest(
    string Username,
    string Password,
    string FullName,
    string Email,
    int RoleId,
    int? BranchId);

public record DarDeBajaRequest(
    int UserId,
    string Reason);

public record ListarUsuariosRequest(
    int? RoleId = null,
    int? BranchId = null,
    int PageNumber = 1,
    int PageSize = 20);
