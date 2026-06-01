namespace GymAffiliate.Application.DTOs.Responses;

// ─────────────────────────────────────────────────────────────────────────────
// Afiliados
// ─────────────────────────────────────────────────────────────────────────────

public class AfiliadoResponse
{
    public int AffiliateId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Phone { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? EmergencyContact { get; set; }
    public string? EmergencyPhone { get; set; }
    public int? BaseBranchId { get; set; }
    public string? BaseBranchName { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime RegistrationDate { get; set; }
    public string? Notes { get; set; }
    public MembresiaResumenResponse? MembresiaVigente { get; set; }
    public PagoResumenResponse? UltimoPago { get; set; }
}

public class AfiliadoListaResponse
{
    public int AffiliateId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string? Phone { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? BaseBranchName { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? CurrentMembership { get; set; }
    public DateTime? MembershipEndDate { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public DateTime RegistrationDate { get; set; }
}

public class CrearAfiliadoResponse
{
    public int AffiliateId { get; set; }
    public string Message { get; set; } = string.Empty;
}
// ─────────────────────────────────────────────────────────────────────────────
// Membresías
// ─────────────────────────────────────────────────────────────────────────────

public class MembresiaResumenResponse
{
    public int MembershipId { get; set; }
    public string? TypeCode { get; set; }
    public string? TypeName { get; set; }
    public string? AccessScope { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public string? BranchName { get; set; }
    public int? RenewalCount { get; set; }
}


public class MembresiaResponse
{
    public int MembershipId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? TypeName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? DaysUntilExpiry { get; set; }
}

public class ValidacionAccesoResponse
{
    public int AffiliateId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsAccessGranted { get; set; }
    public string AccessMessage { get; set; } = string.Empty;
    public string? TypeName { get; set; }
    public string? AccessScope { get; set; }
    public DateTime? EndDate { get; set; }
    public int? DaysUntilExpiry { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Pagos
// ─────────────────────────────────────────────────────────────────────────────

public class PagoResumenResponse
{
    public int? LastPaymentId { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? MethodName { get; set; }
}


public class PagoResponse
{
    public int PaymentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string MembershipType { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}

public class RegistrarPagoResponse
{
    public int PaymentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────────────────────
// Acceso / CheckIn
// ─────────────────────────────────────────────────────────────────────────────

public class CheckInResponse
{
    public int? CheckInId { get; set; }
    public int AffiliateId { get; set; }
    public string AffiliateName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public bool AccessGranted { get; set; }
    public string? DenialReason { get; set; }
    public DateTime? MembershipEndDate { get; set; }
    public DateTime CheckInTime { get; set; }
    public string Message { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────────────────────
// Notificaciones
// ─────────────────────────────────────────────────────────────────────────────

public class VencimientoResponse
{
    public int MembershipId { get; set; }
    public int AffiliateId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string MembershipType { get; set; } = string.Empty;
    public DateTime EndDate { get; set; }
    public int DaysUntilExpiry { get; set; }
    public string? BranchName { get; set; }
    public bool NotificationSent { get; set; }
}

public class NotificacionPendienteResponse
{
    public int NotificationId { get; set; }
    public int AffiliateId { get; set; }
    public string AffiliateName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AlertaEnviadaResponse
{
    public int NotificationsGenerated { get; set; }
    public string Message { get; set; } = string.Empty;
}
// ─────────────────────────────────────────────────────────────────────────────
// Reportes
// ─────────────────────────────────────────────────────────────────────────────

public class IngresoMensualResponse
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public int TotalPayments { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AvgPayment { get; set; }
    public string? BranchName { get; set; }
}

public class AfiliadoEstadoResponse
{
    public string Status { get; set; } = string.Empty;
    public int Total { get; set; }
    public string? BranchName { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Sucursales
// ─────────────────────────────────────────────────────────────────────────────

public class SucursalResponse
{
    public int BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public bool IsActive { get; set; }
    public int TotalAffiliates { get; set; }
    public int ActiveAffiliates { get; set; }
}
