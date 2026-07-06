using FluentValidation;
using GymAffiliate.Application.DTOs.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GymAffiliate.Application.Validations;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator() 
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("El nombre de usuario es requerido.")
            .MaximumLength(100).WithMessage("El username no puede superar 100 caracteres.");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es requerida.")
            .MinimumLength(6).WithMessage("La contraseña debe tener al menos 6 caracteres.");
    }
}

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("El refresh token es requerido.");
    }
}

public class CrearUsuarioRequestValidator : AbstractValidator<CrearUsuarioRequest>
{
    public CrearUsuarioRequestValidator()
    {
        RuleFor (x => x.Username)
            .NotEmpty().WithMessage("El nombre de usuario es requerido.")
            .MinimumLength(3).WithMessage("El username debe tener al menos 3 caracteres.")
            .MaximumLength(100).WithMessage("El username no puede superar 100 caracteres.")
            .Matches(@"^[a-zA-Z0-9._\-]+$").WithMessage("El username solo puede contener letras, números, puntos, guiones y guión bajo.");
        
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es requerida.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .MaximumLength(100).WithMessage("La contraseña no puede superar 100 caracteres.")
            .Matches(@"[A-Z]").WithMessage("La contraseña debe contener al menos una letra mayúscula.")
            .Matches(@"[0-9]").WithMessage("La contraseña debe contener al menos un número.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre completo es requerido.")
            .MaximumLength(200).WithMessage("El nombre no puede superar 200 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es requerido.")
            .EmailAddress().WithMessage("El correo electrónico no tiene un formato válido.")
            .MaximumLength(150).WithMessage("El correo no puede superar 150 caracteres.");

        RuleFor(x => x.RoleId)
            .GreaterThan(0).WithMessage("El rol es requerido.");
    }

}

public class DarDeBajaRequestValidator : AbstractValidator<DarDeBajaRequest>
{
    public DarDeBajaRequestValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("UserId inválido.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("El motivo de baja es obligatorio.")
            .MinimumLength(10).WithMessage("El motivo debe tener al menos 10 caracteres.")
            .MaximumLength(500).WithMessage("El motivo no puede superar 500 caracteres.");
    }
}