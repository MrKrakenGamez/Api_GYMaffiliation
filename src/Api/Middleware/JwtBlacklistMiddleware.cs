using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using GymAffiliate.Application.DTOs.Requests;
using GymAffiliate.Application.DTOs.Responses;
using GymAffiliate.Application.UseCases.Auth;
using GymAffiliate.Domain.Interfaces.Repositories;
using GymAffiliate.Infrastructure.Configuration;
using GymAffiliate.Infrastructure.DependencyInjection;
using GymAffiliate.Infrastructure.Persistence.Repositories;
using GymAffiliate.Shared.Constants;
using GymAffiliate.Shared.Result;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GymAffiliate.Api.Middleware;

public sealed class JwtBlacklistMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, IAuthRepository authRepo)
    {
        // Solo revisar requests con Bearer token
        var authHeader = ctx.Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var tokenStr = authHeader["Bearer ".Length..].Trim();

            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(tokenStr))
                {
                    var jwt = handler.ReadJwtToken(tokenStr);
                    var jti = jwt.Id;

                    if (!string.IsNullOrEmpty(jti))
                    {
                        var result = await authRepo.IsTokenRevokedAsync(jti, ctx.RequestAborted);
                        if (result.IsSuccess && result.Value)
                        {
                            ctx.Response.StatusCode = 401;
                            ctx.Response.ContentType = "application/json";
                            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
                            {
                                success = false,
                                errorCode = "AU_102",
                                message = "El token ha sido revocado."
                            }));
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Si no se puede leer el token, dejar pasar al siguiente middleware
                // que lo rechazará por inválido
            }
        }

        await next(ctx);
    }
}