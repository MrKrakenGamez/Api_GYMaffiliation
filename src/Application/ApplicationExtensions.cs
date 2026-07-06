using FluentValidation;
using GymAffiliate.Application.Common.Mappings;
using GymAffiliate.Application.UseCases.Acceso;
using GymAffiliate.Application.UseCases.Afiliados;
using GymAffiliate.Application.UseCases.Membresias;
using GymAffiliate.Application.UseCases.Notificaciones;
using GymAffiliate.Application.UseCases.Pagos;
using GymAffiliate.Application.UseCases.Reportes;
using GymAffiliate.Application.Common.Mappings;
using Microsoft.Extensions.DependencyInjection;
using GymAffiliate.Application.UseCases.Auth;
using GymAffiliate.Application.DTOs.Requests;
using GymAffiliate.Application.Validations;

namespace GymAffiliate.Application;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // AutoMapper
        //services.AddAutoMapper(typeof(MappingProfile).Assembly);
        services.AddAutoMapper(cfg => { }, typeof(ApplicationExtensions).Assembly);
        //builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

        // FluentValidation — register all validators from Application assembly
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);

        // Use Cases (Transient: stateless, no shared state between requests)
        services.AddTransient<CrearAfiliadoHandler>();
        services.AddTransient<ActualizarAfiliadoHandler>();
        services.AddTransient<EliminarAfiliadoHandler>();
        services.AddTransient<ObtenerAfiliadoHandler>();
        services.AddTransient<ListarAfiliadosHandler>();

        services.AddTransient<AsignarMembresiaHandler>();
        services.AddTransient<RenovarMembresiaHandler>();
        services.AddTransient<CambiarPlanHandler>();

        services.AddTransient<RegistrarPagoHandler>();
        services.AddTransient<HistorialPagosHandler>();

        services.AddTransient<RegistrarIngresoHandler>();
        services.AddTransient<ValidarAccesoHandler>();

        services.AddTransient<VencimientosHandler>();
        services.AddTransient<EnviarAlertaHandler>();

        services.AddTransient<ReporteIngresosHandler>();
        services.AddTransient<AfiliadosActivosHandler>();

        // Auth handlers
        services.AddTransient<LoginHandler>();
        services.AddTransient<RefreshTokenHandler>();
        services.AddTransient<LogoutHandler>();
        services.AddTransient<CrearUsuarioHandler>();
        services.AddTransient<DarDeBajaUsuarioHandler>();
        services.AddTransient<ObtenerUsuarioHandler>();
        services.AddTransient<ListarUsuariosHandler>();
        services.AddTransient<PurgarTokensHandler>();

        // Auth validators
        services.AddTransient<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddTransient<IValidator<RefreshTokenRequest>, RefreshTokenRequestValidator>();
        services.AddTransient<IValidator<CrearUsuarioRequest>, CrearUsuarioRequestValidator>();
        services.AddTransient<IValidator<DarDeBajaRequest>, DarDeBajaRequestValidator>();

        return services;
    }
}
