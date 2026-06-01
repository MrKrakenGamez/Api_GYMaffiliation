using GymAffiliate.Domain.Enums;

namespace GymAffiliate.Domain.Entities;

public sealed class Afiliado
{
    public int AffiliateId { get; private set; }
    public string DocumentNumber { get; private set; } = string.Empty;
    public string DocumentType { get; private set; } = "DNI";
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public DateOnly BirthDate { get; private set; }
    public int Age => CalcularEdad();
    public string? Phone { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public int? BaseBranchId { get; private set; }
    public EstadoAfiliado Status { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime RegistrationDate { get; private set; }

    private readonly List<Membresia> _membresias = [];
    public IReadOnlyCollection<Membresia> Membresias => _membresias.AsReadOnly();
    private Afiliado() { }

    public static Afiliado Reconstituir(
        int id, string docNumber, string docType,
        string firstName, string lastName, DateOnly birthDate,
        string? phone, string email, string? address,
        int? baseBranchId, EstadoAfiliado status, bool isDeleted,
        DateTime registrationDate) => new()
    {
        AffiliateId = id, DocumentNumber = docNumber, DocumentType = docType,
        FirstName = firstName, LastName = lastName, BirthDate = birthDate,
        Phone = phone, Email = email, Address = address,
        BaseBranchId = baseBranchId, Status = status, IsDeleted = isDeleted,
        RegistrationDate = registrationDate
    };

    public void AgregarMembresia(Membresia m) => _membresias.Add(m);

    public Membresia? ObtenerMembresiaVigente() =>
        _membresias.FirstOrDefault(m =>
            m.Status == EstadoMembresia.Activa &&
            m.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow));

    public bool PuedeIngresarA(int branchId)
    {
        if (IsDeleted || Status == EstadoAfiliado.Suspendido) return false;
        var m = ObtenerMembresiaVigente();
        return m is not null && m.PermiteAccesoA(branchId);
    }

    private int CalcularEdad()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - BirthDate.Year;
        if (today < BirthDate.AddYears(age)) age--;
        return age;
    }
}

public sealed class Membresia
{
    public int MembershipId { get; private set; }
    public int AffiliateId { get; private set; }
    public int MembershipTypeId { get; private set; }
    public string? TypeCode { get; private set; }
    public string? TypeName { get; private set; }
    public TipoAcceso AccessScope { get; private set; }
    public int? BranchId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public EstadoMembresia Status { get; private set; }
    public int RenewalCount { get; private set; }
    public int DaysUntilExpiry =>
        Math.Max(0, EndDate.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber);

    private Membresia() { }

    public static Membresia Reconstituir(
        int id, int affiliateId, int typeId,
        string? typeCode, string? typeName, string accessScope,
        int? branchId, DateOnly startDate, DateOnly endDate,
        EstadoMembresia status, int renewalCount) => new()
    {
        MembershipId = id, AffiliateId = affiliateId, MembershipTypeId = typeId,
        TypeCode = typeCode, TypeName = typeName,
        AccessScope = accessScope == "ALL_BRANCHES" ? TipoAcceso.TodasSucursales : TipoAcceso.SucursalUnica,
        BranchId = branchId, StartDate = startDate, EndDate = endDate,
        Status = status, RenewalCount = renewalCount
    };

    public bool EstaVigente() =>
        Status == EstadoMembresia.Activa && EndDate >= DateOnly.FromDateTime(DateTime.UtcNow);

    public bool PermiteAccesoA(int branchId) =>
        EstaVigente() && (AccessScope == TipoAcceso.TodasSucursales || BranchId == branchId);
}

public sealed class Sucursal
{
    public int BranchId { get; private set; }
    public string BranchCode { get; private set; } = string.Empty;
    public string BranchName { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    private Sucursal() { }
    public static Sucursal Reconstituir(int id, string code, string name, string address, bool active) =>
        new() { BranchId = id, BranchCode = code, BranchName = name, Address = address, IsActive = active };
}
