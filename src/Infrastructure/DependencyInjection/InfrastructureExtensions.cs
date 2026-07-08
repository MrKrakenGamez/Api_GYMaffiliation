using System.Text;
using GymAffiliate.Domain.Interfaces.Repositories;
using GymAffiliate.Domain.Interfaces.Services;
using GymAffiliate.Infrastructure.Configuration;
using GymAffiliate.Infrastructure.Persistence.Dapper.Context;
using GymAffiliate.Infrastructure.Persistence.Repositories;
using GymAffiliate.Infrastructure.Services;
using GymAffiliate.Shared.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace GymAffiliate.Infrastructure.DependencyInjection;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Options ───────────────────────────────────────────────────────
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

        // ── Dapper context ────────────────────────────────────────────────
        services.AddScoped<IDapperContext, DapperContext>();

        // ── Repositories ──────────────────────────────────────────────────
        services.AddScoped<IAfiliadoRepository, AfiliadoRepository>();
        services.AddScoped<IMembresiaRepository, MembresiaRepository>();
        services.AddScoped<IPagoRepository, PagoRepository>();
        services.AddScoped<IAccesoRepository, AccesoRepository>();
        services.AddScoped<INotificacionRepository, NotificacionRepository>();
        services.AddScoped<ISucursalRepository, SucursalRepository>();
        services.AddScoped<IReporteRepository, ReporteRepository>();
        services.AddScoped<IAuthRepository, AuthRepository>();

        // ── Services ──────────────────────────────────────────────────────
        services.AddSingleton<ITokenService, TokenService>();

        // ── JWT Authentication (una sola vez) ─────────────────────────────
        var authOpts = configuration
            .GetSection(AuthOptions.Section)
            .Get<AuthOptions>() ?? new AuthOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = authOpts.JwtSettings.Issuer,
                    ValidAudience = authOpts.JwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(authOpts.JwtSettings.Secret)),
                    ClockSkew = TimeSpan.Zero
                };

                opts.Events = new JwtBearerEvents
                {


                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Headers["Authorization"].ToString();

                        Console.WriteLine($"TOKEN: {token}");

                        return Task.CompletedTask;
                    },

                    OnAuthenticationFailed = ctx =>
                    {
                        Console.WriteLine($"ERROR JWT: {ctx.Exception}");

                        return Task.CompletedTask;
                    },

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

        // ── Authorization policies (una sola vez) ─────────────────────────
        services.AddAuthorization(opts =>
        {
            opts.AddPolicy(Policies.SuperAdminOnly,
                p => p.RequireRole(Roles.SuperAdmin));
            opts.AddPolicy(Policies.AdminOnly,
                p => p.RequireRole(Roles.SuperAdmin, Roles.Admin));
            opts.AddPolicy(Policies.ReceptionOrAdmin,
                p => p.RequireRole(Roles.SuperAdmin, Roles.Admin, Roles.Reception));
            opts.AddPolicy(Policies.AnyRole,
                p => p.RequireAuthenticatedUser());
        });

        return services;
    }
}
