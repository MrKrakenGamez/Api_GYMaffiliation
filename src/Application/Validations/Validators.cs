using FluentValidation;
using GymAffiliate.Application.DTOs.Requests;

namespace GymAffiliate.Application.Validations;

public class CrearAfiliadoValidator : AbstractValidator<CrearAfiliadoRequest>
{
    //private static readonly string[] TiposDocumento = ["DNI", "PASSPORT", "OTHER","INE"];
    private static readonly string[] TiposDocumento = ["DNI", "PASAPORTE", "CEDULA", "INE"];

    public CrearAfiliadoValidator()
    {
        RuleFor(x => x.DocumentNumber)
            .NotEmpty().WithMessage("El número de documento es requerido.")
            .MaximumLength(30).WithMessage("El documento no puede exceder 30 caracteres.");

        RuleFor(x => x.DocumentType)
            .NotEmpty().WithMessage("El tipo de documento es requerido.")
            .Must(t => TiposDocumento.Contains(t?.ToUpperInvariant()))
            .WithMessage("Tipo de documento debe ser: DNI, PASSPORT u OTHER.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es requerido.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("El apellido es requerido.")
            .MaximumLength(100).WithMessage("El apellido no puede exceder 100 caracteres.");

        RuleFor(x => x.BirthDate)
            .NotEmpty().WithMessage("La fecha de nacimiento es requerida.")
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-14)))
            .WithMessage("El afiliado debe tener al menos 14 años.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es requerido.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(150).WithMessage("El email no puede exceder 150 caracteres.");

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("El teléfono no puede exceder 30 caracteres.")
            .When(x => x.Phone is not null);

        RuleFor(x => x.Address)
            .MaximumLength(300).When(x => x.Address is not null);
    }
}

public class ActualizarAfiliadoValidator : AbstractValidator<ActualizarAfiliadoRequest>
{
    public ActualizarAfiliadoValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(150)
            .When(x => x.Email is not null);

        RuleFor(x => x.BirthDate)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-14)))
            .WithMessage("El afiliado debe tener al menos 14 años.")
            .When(x => x.BirthDate.HasValue);

        RuleFor(x => x.Phone)
            .MaximumLength(30).When(x => x.Phone is not null);
    }
}

public class AsignarMembresiaValidator : AbstractValidator<AsignarMembresiaRequest>
{
    public AsignarMembresiaValidator()
    {
        RuleFor(x => x.AffiliateId)
            .GreaterThan(0).WithMessage("AffiliateId debe ser mayor a 0.");

        RuleFor(x => x.MembershipTypeId)
            .GreaterThan(0).WithMessage("MembershipTypeId debe ser mayor a 0.");
    }
}

public class RenovarMembresiaValidator : AbstractValidator<RenovarMembresiaRequest>
{
    public RenovarMembresiaValidator()
    {
        RuleFor(x => x.AffiliateId)
            .GreaterThan(0).WithMessage("AffiliateId debe ser mayor a 0.");
    }
}

public class CambiarPlanValidator : AbstractValidator<CambiarPlanRequest>
{
    public CambiarPlanValidator()
    {
        RuleFor(x => x.AffiliateId)
            .GreaterThan(0).WithMessage("AffiliateId debe ser mayor a 0.");

        RuleFor(x => x.NewMembershipTypeId)
            .GreaterThan(0).WithMessage("NewMembershipTypeId debe ser mayor a 0.");
    }
}

public class RegistrarPagoValidator : AbstractValidator<RegistrarPagoRequest>
{
    public RegistrarPagoValidator()
    {
        RuleFor(x => x.AffiliateId)
            .GreaterThan(0).WithMessage("AffiliateId debe ser mayor a 0.");

        RuleFor(x => x.MembershipId)
            .GreaterThan(0).WithMessage("MembershipId debe ser mayor a 0.");

        RuleFor(x => x.PaymentMethodId)
            .GreaterThan(0).WithMessage("PaymentMethodId debe ser mayor a 0.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");
    }
}

public class RegistrarIngresoValidator : AbstractValidator<RegistrarIngresoRequest>
{
    public RegistrarIngresoValidator()
    {
        RuleFor(x => x.AffiliateId)
            .GreaterThan(0).WithMessage("AffiliateId debe ser mayor a 0.");

        RuleFor(x => x.BranchId)
            .GreaterThan(0).WithMessage("BranchId debe ser mayor a 0.");
    }
}

public class EnviarAlertaValidator : AbstractValidator<EnviarAlertaRequest>
{
    private static readonly string[] Canales = ["EMAIL", "SMS", "SYSTEM"];

    public EnviarAlertaValidator()
    {
        RuleFor(x => x.DaysAhead)
            .InclusiveBetween(1, 30).WithMessage("DaysAhead debe estar entre 1 y 30.");

        RuleFor(x => x.Channel)
            .Must(c => Canales.Contains(c?.ToUpperInvariant()))
            .WithMessage("Channel debe ser EMAIL, SMS o SYSTEM.");
    }
}
