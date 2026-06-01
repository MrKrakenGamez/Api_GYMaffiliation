using System.Net;
using System.Text.Json;
using GymAffiliate.Domain.Exceptions;
using GymAffiliate.Shared.Errors;
using Microsoft.Data.SqlClient;

namespace GymAffiliate.Api.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> log)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Excepción no controlada: {Message}", ex.Message);
            await HandleAsync(ctx, ex);
        }
    }

    private static async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        var (code, message, status) = ex switch
        {
            Domain.Exceptions.ValidationException ve =>
                (ErrorCodes.ErrorValidacion, ve.Message, (int)HttpStatusCode.UnprocessableEntity),

            NotFoundException nfe =>
                (ErrorCodes.AfiliadoNoEncontrado, nfe.Message, (int)HttpStatusCode.NotFound),

            BusinessRuleException bre =>
                (bre.ErrorCode, bre.Message, (int)HttpStatusCode.BadRequest),

            UnauthorizedException =>
                (ErrorCodes.NoAutorizado, ex.Message, (int)HttpStatusCode.Unauthorized),

            DomainException =>
                (ErrorCodes.ErrorInesperado, ex.Message, (int)HttpStatusCode.BadRequest),

            SqlException sqle when sqle.Number == -2 || sqle.Number == 53 =>
                (ErrorCodes.ConexionBD, "No se pudo conectar a la base de datos.", (int)HttpStatusCode.ServiceUnavailable),

            SqlException =>
                (ErrorCodes.ConexionBD, "Error en la base de datos.", (int)HttpStatusCode.InternalServerError),

            OperationCanceledException =>
                (ErrorCodes.ErrorInesperado, "Operación cancelada.", 499),

            _ => (ErrorCodes.ErrorInesperado, "Error interno del servidor.", (int)HttpStatusCode.InternalServerError)
        };

        var details = ex is Domain.Exceptions.ValidationException valEx ? valEx.Errors : null;

        var body = new
        {
            success = false,
            error = new
            {
                code,
                message,
                status,
                timestamp = DateTimeOffset.UtcNow,
                traceId   = ctx.TraceIdentifier,
                details
            }
        };

        ctx.Response.StatusCode  = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOpts));
    }
}
