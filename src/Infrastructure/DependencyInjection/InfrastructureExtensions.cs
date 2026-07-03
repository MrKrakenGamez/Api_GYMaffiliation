using System.Text;
using GymAffiliate.Application.UseCases.Auth;
using GymAffiliate.Domain.Interfaces.Repositories;
using GymAffiliate.Infrastructure.Configuration;
using GymAffiliate.Infrastructure.Persistence.Dapper.Context;
using GymAffiliate.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GymAffiliate.Infrastructure.DependencyInjection;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Options (validated at startup) ────────────────────────────────
        services.AddOptions<ConnectionStringOptions>()
            .Bind(configuration.GetSection(ConnectionStringOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.Section));

        services.AddOptions<NotificationOptions>()
            .Bind(configuration.GetSection(NotificationOptions.Section));

        // ── Dapper context (Scoped = per request) ─────────────────────────
        services.AddScoped<IDapperContext, DapperContext>();

        // ── Repositories ──────────────────────────────────────────────────
        services.AddScoped<IAfiliadoRepository,    AfiliadoRepository>();
        services.AddScoped<IMembresiaRepository,   MembresiaRepository>();
        services.AddScoped<IPagoRepository,        PagoRepository>();
        services.AddScoped<IAccesoRepository,      AccesoRepository>();
        services.AddScoped<INotificacionRepository, NotificacionRepository>();
        services.AddScoped<ISucursalRepository,    SucursalRepository>();
        services.AddScoped<IReporteRepository,     ReporteRepository>();
        // Auth
           services.AddScoped<IAuthRepository, AuthRepository>();
           services.AddSingleton<TokenService>();

        // ── JWT Authentication (prepared, toggle with UseJwt flag) ────────
        var authOpts = configuration.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();

          // JWT Authentication
    var jwtSettings = configuration.GetSection("Auth:JwtSettings").Get<JwtSettings>()!;
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.TokenValidationParameters = new TokenService(
                    Microsoft.Extensions.Options.Options.Create(
                        configuration.GetSection(AuthOptions.Section).Get<AuthOptions>()!))
                    .GetValidationParameters();

                opts.Events = new JwtBearerEvents
                {
                    OnChallenge = ctx =>
                    {
                        ctx.HandleResponse();
                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = "application/json";
                        return ctx.Response.WriteAsync(
                            "{\"success\":false,\"errorCode\":\"AU_101\",\"message\":\"No autenticado.\"}");
                    },
                    OnForbidden = ctx =>
                    {
                        ctx.Response.StatusCode = 403;
                        ctx.Response.ContentType = "application/json";
                        return ctx.Response.WriteAsync(
                            "{\"success\":false,\"errorCode\":\"AU_108\",\"message\":\"Sin permisos.\"}");
                    }
                };
            });


        if (authOpts.UseJwt && !string.IsNullOrWhiteSpace(authOpts.JwtSettings.Secret))
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer           = true,
                        ValidateAudience         = true,
                        ValidateLifetime         = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer              = authOpts.JwtSettings.Issuer,
                        ValidAudience            = authOpts.JwtSettings.Audience,
                        IssuerSigningKey         = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(authOpts.JwtSettings.Secret)),
                        ClockSkew = TimeSpan.Zero
                    };
                });
        }
        else
        {
            // Stub authentication for development (no real auth required)
            services.AddAuthentication();
        }

        // ── Authorization policies ────────────────────────────────────────
        services.AddAuthorization(opts =>
        {
            opts.AddPolicy(Shared.Constants.Policies.AdminOnly,
                p => p.RequireRole(Shared.Constants.Roles.Admin, Shared.Constants.Roles.SuperAdmin));
            opts.AddPolicy(Shared.Constants.Policies.ReceptionOrAdmin,
                p => p.RequireRole(Shared.Constants.Roles.Reception, Shared.Constants.Roles.Admin, Shared.Constants.Roles.SuperAdmin));
            opts.AddPolicy(Shared.Constants.Policies.AnyRole,
                p => p.RequireAuthenticatedUser());
        });

        return services;
    }
}
