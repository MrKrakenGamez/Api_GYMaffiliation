using GymAffiliate.Shared.Result;

namespace GymAffiliate.Domain.Interfaces.Repositories;

// ─────────────────────────────────────────────────────────────────────────────
// Interfaces
// ─────────────────────────────────────────────────────────────────────────────

public interface IAfiliadoRepository
{
    Task<Result<int>> CrearAsync(CrearAfiliadoParams p, CancellationToken ct = default);
    Task<Result<int>> ActualizarAsync(ActualizarAfiliadoParams p, CancellationToken ct = default);
    Task<Result> EliminarAsync(int affiliateId, string? notes, int? userId, string? ip, string? session, CancellationToken ct = default);
    Task<Result<AfiliadoDetalleRaw?>> ObtenerAsync(int? id, string? doc, string? email, int? userId, CancellationToken ct = default);
    Task<Result<(IEnumerable<AfiliadoListaRaw> Items, int Total)>> ListarAsync(ListarAfiliadosParams p, CancellationToken ct = default);
}

public interface IMembresiaRepository
{
    Task<Result<int>> AsignarAsync(AsignarMembresiaParams p, CancellationToken ct = default);
    Task<Result<int>> RenovarAsync(RenovarMembresiaParams p, CancellationToken ct = default);
    Task<Result<int>> CambiarPlanAsync(CambiarPlanParams p, CancellationToken ct = default);
    Task<Result> CancelarAsync(int? membershipId, int? affiliateId, string? notes, int? userId, CancellationToken ct = default);
    Task<Result<ValidacionAccesoRaw?>> ValidarActivaAsync(int affiliateId, CancellationToken ct = default);
}

public interface IPagoRepository
{
    Task<Result<(int PaymentId, string Receipt)>> RegistrarAsync(RegistrarPagoParams p, CancellationToken ct = default);
    Task<Result> CancelarAsync(int paymentId, string? notes, int? userId, string? ip, string? session, CancellationToken ct = default);
    Task<Result<IEnumerable<PagoListaRaw>>> ListarAsync(int? affiliateId, DateOnly? from, DateOnly? to, int? branchId, int? userId, CancellationToken ct = default);
}

public interface IAccesoRepository
{
    Task<Result<CheckInRaw>> RegistrarIngresoAsync(int affiliateId, int branchId, int? userId, string? ip, string? session, CancellationToken ct = default);
    Task<Result<ValidacionAccesoRaw?>> ValidarAccesoAsync(int affiliateId, CancellationToken ct = default);
}

public interface INotificacionRepository
{
    Task<Result<IEnumerable<VencimientoRaw>>> ObtenerPorVencerAsync(int daysAhead, int? userId, CancellationToken ct = default);
    Task<Result<IEnumerable<NotificacionPendienteRaw>>> ObtenerPendientesAsync(int? affiliateId, CancellationToken ct = default);
    Task<Result<int>> GenerarAlertasAsync(int daysAhead, string channel, int? userId, string? session, CancellationToken ct = default);
    Task<Result> MarcarEnviadaAsync(int notificationId, string? errorDetail, int? userId, CancellationToken ct = default);
}

public interface ISucursalRepository
{
    Task<Result<IEnumerable<SucursalRaw>>> ListarAsync(int? branchId, CancellationToken ct = default);
}

public interface IReporteRepository
{
    Task<Result<IEnumerable<IngresoMensualRaw>>> IngresosAsync(int? year, int? month, int? branchId, int? userId, CancellationToken ct = default);
    Task<Result<IEnumerable<AfiliadoEstadoRaw>>> AfiliadosActivosAsync(int? branchId, int? userId, CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// Parameter Records
// ─────────────────────────────────────────────────────────────────────────────

public record CrearAfiliadoParams(
    string DocumentNumber, string DocumentType,
    string FirstName, string LastName, DateOnly BirthDate,
    string Email, string? Phone, string? Address,
    string? EmergencyContact, string? EmergencyPhone,
    int? BaseBranchId, string? Notes,
    int? UserId, string? Ip, string? Session);

public record ActualizarAfiliadoParams(
    int AffiliateId,
    string? FirstName, string? LastName, DateOnly? BirthDate,
    string? NewEmail, string? Phone, string? Address,
    string? EmergencyContact, string? EmergencyPhone,
    int? BaseBranchId, string? Notes,
    int? UserId, string? Ip, string? Session);

public record ListarAfiliadosParams(
    int? FilterStatus, int? FilterBranchId, string? FilterSearch,
    int PageNumber, int PageSize, int? UserId);

public record AsignarMembresiaParams(
    int AffiliateId, int MembershipTypeId, int? BranchId,
    DateOnly? StartDate, string? Notes,
    int? UserId, string? Ip, string? Session);

public record RenovarMembresiaParams(
    int AffiliateId, int? MembershipTypeId, int? BranchId, string? Notes,
    int? UserId, string? Ip, string? Session);

public record CambiarPlanParams(
    int AffiliateId, int NewMembershipTypeId, int? BranchId,
    DateOnly? StartDate, int? UserId, string? Ip, string? Session);

public record RegistrarPagoParams(
    int AffiliateId, int MembershipId, int PaymentMethodId,
    decimal Amount, string? ReferenceNumber, string? Notes,
    int? UserId, string? Ip, string? Session);

// ─────────────────────────────────────────────────────────────────────────────
// Raw Data Models
// TODOS los campos opcionales del SP deben ser NULLABLE aqui.
// Dapper mapea NULL de SQL a null de C# solo si el tipo es nullable.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Datos del afiliado + membresía vigente + último pago.
/// Devuelto por sp_Affiliates / viewaffiliated (RS1).
/// Los campos de membresía y pago son nullable porque el afiliado puede no tener ninguna.
/// </summary>
public class AfiliadoDetalleRaw
{
    public int       AffiliateId        { get; set; }
    public string    DocumentNumber     { get; set; } = string.Empty;
    public string    DocumentType       { get; set; } = string.Empty;
    public string    FirstName          { get; set; } = string.Empty;
    public string    LastName           { get; set; } = string.Empty;
    public string    FullName           => $"{FirstName} {LastName}";
    public int       Age                { get; set; }
    public DateTime  BirthDate          { get; set; }
    public string?   Phone              { get; set; }
    public string    Email              { get; set; } = string.Empty;
    public string?   Address            { get; set; }
    public string?   EmergencyContact   { get; set; }
    public string?   EmergencyPhone     { get; set; }
    public int?      BaseBranchId       { get; set; }
    public string?   BaseBranchName     { get; set; }
    public byte      StatusId           { get; set; }
    public string    StatusName         { get; set; } = string.Empty;
    public DateTime  RegistrationDate   { get; set; }
    public string?   Notes              { get; set; }
    // Membresía vigente (null si no tiene)
    public int?      MembershipId       { get; set; }
    public string?   TypeCode           { get; set; }
    public string?   MembershipTypeName { get; set; }
    public string?   AccessScope        { get; set; }
    public DateTime? StartDate          { get; set; }
    public DateTime? EndDate            { get; set; }
    public int?      DaysUntilExpiry    { get; set; }
    public int?      MembershipBranchId { get; set; }
    public string?   MembershipBranchName { get; set; }
    public int?      RenewalCount       { get; set; }
    // Último pago (null si no tiene)
    public int?      LastPaymentId      { get; set; }
    public decimal?  LastPaymentAmount  { get; set; }
    public DateTime? LastPaymentDate    { get; set; }
    public string?   LastPaymentMethod  { get; set; }
}

/// <summary>Fila del listado paginado de afiliados (sp_Affiliates / listaffiliated).</summary>
public class AfiliadoListaRaw
{
    public int       AffiliateId      { get; set; }
    public string    DocumentNumber   { get; set; } = string.Empty;
    public string    FullName         { get; set; } = string.Empty;
    public int       Age              { get; set; }
    public string?   Phone            { get; set; }
    public string    Email            { get; set; } = string.Empty;
    public string?   BaseBranchName   { get; set; }
    public byte      StatusId         { get; set; }
    public string    StatusName       { get; set; } = string.Empty;
    public string?   CurrentMembership{ get; set; }
    public DateTime? MembershipEndDate{ get; set; }
    public int?      DaysUntilExpiry  { get; set; }
    public DateTime  RegistrationDate { get; set; }
    public int       TotalRecords     { get; set; }
}

public class MembresiaHistorialRaw
{
    public int      MembershipId { get; set; }
    public string   TypeName     { get; set; } = string.Empty;
    public DateTime StartDate    { get; set; }
    public DateTime EndDate      { get; set; }
    public string   StatusName   { get; set; } = string.Empty;
    public int      RenewalCount { get; set; }
    public DateTime CreatedAt    { get; set; }
}

public class PagoHistorialRaw
{
    public int      PaymentId      { get; set; }
    public decimal  Amount         { get; set; }
    public DateTime PaymentDate    { get; set; }
    public string   MethodName     { get; set; } = string.Empty;
    public string?  ReferenceNumber{ get; set; }
    public string?  ReceiptNumber  { get; set; }
    public string   PaymentStatus  { get; set; } = string.Empty;
}

public class ValidacionAccesoRaw
{
    public int      AffiliateId       { get; set; }
    public string   FullName          { get; set; } = string.Empty;
    public byte     AffiliateStatus   { get; set; }
    public int?     MembershipId      { get; set; }
    public string?  TypeCode          { get; set; }
    public string?  TypeName          { get; set; }
    public string?  AccessScope       { get; set; }
    public int?     MembershipBranchId{ get; set; }
    public DateTime? StartDate        { get; set; }
    public DateTime? EndDate          { get; set; }
    public int?     DaysUntilExpiry   { get; set; }
    public bool     IsAccessGranted   { get; set; }
    public string   AccessMessage     { get; set; } = string.Empty;
}

public class CheckInRaw
{
    public int?     CheckInId         { get; set; }
    public int      AffiliateId       { get; set; }
    public string   AffiliateName     { get; set; } = string.Empty;
    public int      BranchId          { get; set; }
    public bool     AccessGranted     { get; set; }
    public string?  DenialReason      { get; set; }
    public DateTime? MembershipEndDate{ get; set; }
    public DateTime CheckInTime       { get; set; }
    public int?     ErrorId           { get; set; }
}

public class PagoListaRaw
{
    public int      PaymentId      { get; set; }
    public string   ReceiptNumber  { get; set; } = string.Empty;
    public string   AffiliateName  { get; set; } = string.Empty;
    public string   DocumentNumber { get; set; } = string.Empty;
    public string   PaymentMethod  { get; set; } = string.Empty;
    public decimal  Amount         { get; set; }
    public DateTime PaymentDate    { get; set; }
    public string?  ReferenceNumber{ get; set; }
    public string   MembershipType { get; set; } = string.Empty;
    public string?  BranchName     { get; set; }
    public string   PaymentStatus  { get; set; } = string.Empty;
    public int      TotalRecords   { get; set; }
    public decimal  TotalAmount    { get; set; }
}

public class VencimientoRaw
{
    public int      MembershipId   { get; set; }
    public int      AffiliateId    { get; set; }
    public string   FullName       { get; set; } = string.Empty;
    public string?  Phone          { get; set; }
    public string?  Email          { get; set; }
    public string   MembershipType { get; set; } = string.Empty;
    public DateTime EndDate        { get; set; }
    public int      DaysUntilExpiry{ get; set; }
    public string?  BranchName     { get; set; }
    public bool     NotificationSent{ get; set; }
}

public class NotificacionPendienteRaw
{
    public int      NotificationId  { get; set; }
    public int      AffiliateId     { get; set; }
    public string   AffiliateName   { get; set; } = string.Empty;
    public string?  Email           { get; set; }
    public string   NotificationType{ get; set; } = string.Empty;
    public string   Channel         { get; set; } = string.Empty;
    public string?  Subject         { get; set; }
    public string?  Body            { get; set; }
    public int      AttemptCount    { get; set; }
    public DateTime CreatedAt       { get; set; }
    public DateTime? MembershipEndDate { get; set; }
    public int?     DaysUntilExpiry { get; set; }
}

public class SucursalRaw
{
    public int      BranchId        { get; set; }
    public string   BranchCode      { get; set; } = string.Empty;
    public string   BranchName      { get; set; } = string.Empty;
    public string   Address         { get; set; } = string.Empty;
    public string?  Phone           { get; set; }
    public string?  Email           { get; set; }
    public string?  City            { get; set; }
    public string?  State           { get; set; }
    public bool     IsActive        { get; set; }
    public DateTime CreatedAt       { get; set; }
    public int      TotalAffiliates { get; set; }
    public int      ActiveAffiliates{ get; set; }
}

public class IngresoMensualRaw
{
    public int      Year          { get; set; }
    public int      Month         { get; set; }
    public string   PaymentMethod { get; set; } = string.Empty;
    public int      TotalPayments { get; set; }
    public decimal  TotalRevenue  { get; set; }
    public decimal  AvgPayment    { get; set; }
    public string?  BranchName    { get; set; }
}

public class AfiliadoEstadoRaw
{
    public string  Status     { get; set; } = string.Empty;
    public int     Total      { get; set; }
    public string? BranchName { get; set; }
}
