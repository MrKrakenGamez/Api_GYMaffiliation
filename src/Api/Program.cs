using GymAffiliate.Api.Middleware;
using GymAffiliate.Application;
using GymAffiliate.Infrastructure.DependencyInjection;
using Microsoft.OpenApi.Models;
using Serilog;

// ─────────────────────────────────────────────────────────────────────────────
// Bootstrap Serilog antes de que inicie el host
// ─────────────────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando GymAffiliate API...");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ──────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/gymaffiliate-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");
    });

    // ── Application layer ────────────────────────────────────────────────────
    builder.Services.AddApplication();

    // ── Infrastructure layer (Dapper, repos, auth) ───────────────────────────
    builder.Services.AddInfrastructure(builder.Configuration);

    // ── Controllers ──────────────────────────────────────────────────────────
    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.PropertyNamingPolicy =
                System.Text.Json.JsonNamingPolicy.CamelCase;
            opts.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    // ── Swagger / OpenAPI ────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opts =>
    {
        opts.SwaggerDoc("v1", new OpenApiInfo
        {
            Title       = "GymAffiliate Manager API",
            Version     = "v1",
            Description = "API REST para gestión de afiliados, membresías, pagos y acceso al gimnasio.",
            Contact     = new OpenApiContact { Name = "GymAffiliate", Email = "dev@gymaffiliate.com" }
        });

        // JWT security definition (preparado para cuando se active)
        opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name         = "Authorization",
            Type         = SecuritySchemeType.Http,
            Scheme       = "bearer",
            BearerFormat = "JWT",
            In           = ParameterLocation.Header,
            Description  = "Ingresa el token JWT: Bearer {token}"
        });

        opts.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // ── CORS (desarrollo local) ──────────────────────────────────────────────
    builder.Services.AddCors(opts =>
    {
        opts.AddPolicy("DevCors", policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:4200",   // Angular dev server
                    "http://localhost:3000",   // React / Next (por si acaso)
                    "https://localhost:7001")  // HTTPS local
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });

        // Política de producción más restrictiva
        opts.AddPolicy("ProdCors", policy =>
        {
            policy
                .WithOrigins(builder.Configuration
                    .GetSection("AllowedOrigins").Get<string[]>() ?? [])
                .AllowAnyHeader()
                .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH");
        });
    });

    // ── HttpContext accessor (para IP/TraceId en controladores) ─────────────
    builder.Services.AddHttpContextAccessor();

    // ─────────────────────────────────────────────────────────────────────────
    // Build
    // ─────────────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Middleware pipeline ───────────────────────────────────────────────────

    // 1. Global exception handler (siempre primero)
    //app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<JwtBlacklistMiddleware>();

    // 2. Swagger (solo en Development)
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(opts =>
        {
            opts.SwaggerEndpoint("/swagger/v1/swagger.json", "GymAffiliate API v1");
            opts.RoutePrefix = string.Empty;   // Swagger en la raíz: https://localhost:xxxx/
            opts.DisplayRequestDuration();
            opts.EnableTryItOutByDefault();
        });
    }

    // 3. HTTPS redirect
    app.UseHttpsRedirection();

    // 4. CORS
    app.UseCors(app.Environment.IsDevelopment() ? "DevCors" : "ProdCors");

    // 5. Serilog request logging
    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0.0}ms)";
        opts.EnrichDiagnosticContext = (diag, httpCtx) =>
        {
            diag.Set("RequestHost",  httpCtx.Request.Host.Value);
            diag.Set("RequestScheme", httpCtx.Request.Scheme);
            diag.Set("UserAgent",    httpCtx.Request.Headers.UserAgent.ToString());
        };
    });

    // 6. Auth (orden importa: primero Authentication, luego Authorization)
    app.UseAuthentication();
    app.UseAuthorization();

    // 7. Map controllers
    app.MapControllers();

    // 8. Health check mínimo
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
       .WithTags("Health");

    Log.Information("GymAffiliate API lista. Swagger: {Url}", "https://localhost:xxxx/");
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Error fatal al iniciar GymAffiliate API.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
