using GymAffiliate.Application.DTOs.Requests;
using GymAffiliate.Application.UseCases.Auth;
using GymAffiliate.Domain.Interfaces.Repositories;
using GymAffiliate.Shared.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GymAffiliate.Api.Controllers;


[Route("api/auth")]
public sealed class AuthController(
    LoginHandler loginHandler,
    RefreshTokenHandler refreshHandler,
    LogoutHandler logoutHandler,
    CrearUsuarioHandler crearUsuarioHandler,
    DarDeBajaUsuarioHandler darDeBajaHandler,
    ObtenerUsuarioHandler obtenerHandler,
    ListarUsuariosHandler listarHandler,
    PurgarTokensHandler purgarHandler,
    IAuthRepository authRepo) : GymBaseController
{
    // ── POST /api/auth/login ─────────────────────────────────────────────────
    /// <summary>Autenticación de administradores y recepcionistas.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await loginHandler.HandleAsync(request, ClientIp, UserAgent, ct);
        return ToAction(result);
    }

    // ── POST /api/auth/refresh ───────────────────────────────────────────────
    /// <summary>Renueva el Access Token usando un Refresh Token válido.</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken ct)
    {
        var result = await refreshHandler.HandleAsync(request, ClientIp, UserAgent, ct);
        return ToAction(result);
    }

    // ── POST /api/auth/logout ────────────────────────────────────────────────
    /// <summary>Cierra la sesión actual (o todas las sesiones del usuario).</summary>
    [HttpPost("logout")]
    //[Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken ct)
    {
        var result = await logoutHandler.HandleAsync(
            request.RefreshToken, CurrentUserId, request.LogoutAll, ct);

        // También revocar el Access Token actual
        if (result.IsSuccess)
        {
            var jti = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                var expClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Exp)?.Value;
                if (long.TryParse(expClaim, out var expUnix))
                {
                    var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                    await authRepo.RevokeAccessTokenAsync(jti, expDate, CurrentUserId, "Logout.", ct);
                }
            }
        }

        return ToAction(result);
    }

    // ── POST /api/auth/usuarios ──────────────────────────────────────────────
    /// <summary>Crea un nuevo usuario del sistema (Admin o SuperAdmin).</summary>
    [HttpPost("usuarios")]
    //[Microsoft.AspNetCore.Authorization.Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
    public async Task<IActionResult> CrearUsuario(
        [FromBody] CrearUsuarioRequest request,
        CancellationToken ct)

    {
        //if (CurrentUserId is null)
        //    return Unauthorized();
        //var result = await crearUsuarioHandler.HandleAsync(request, CurrentUserId.Value, ct);
        //-- quite la validacion de usuario, de momento cualquiera puede crear usuarios
        var result = await crearUsuarioHandler.HandleAsync(request, 1, ct);
        return ToAction(result);
    }

    // ── DELETE /api/auth/usuarios/{id} ───────────────────────────────────────
    /// <summary>Da de baja lógica a un usuario del sistema.</summary>
    [HttpDelete("usuarios/{id:int}")]
    //[Microsoft.AspNetCore.Authorization.Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
    public async Task<IActionResult> DarDeBaja(
        int id,
        [FromBody] DarDeBajaRequest request,
        CancellationToken ct)
    {
        //if (CurrentUserId is null) return Unauthorized();

        // Asegurar que el UserId del body coincide con la ruta
        var req = request with { UserId = id };
        //var result = await darDeBajaHandler.HandleAsync(req, CurrentUserId.Value, ct);

        var result = await darDeBajaHandler.HandleAsync(req, 1, ct);
        return ToAction(result);
    }

    // ── GET /api/auth/usuarios/{id} ──────────────────────────────────────────
    /// <summary>Obtiene los datos de un usuario del sistema por Id.</summary>
    [HttpGet("usuarios/{id:int}")]
    //[Microsoft.AspNetCore.Authorization.Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
    public async Task<IActionResult> ObtenerUsuario(int id, CancellationToken ct)
    {
        var result = await obtenerHandler.HandleAsync(id, null, ct);
        if (result.IsSuccess && result.Value is null)
            return NotFound(new { message = "Usuario no encontrado." });
        return ToAction(result);
    }

    // ── GET /api/auth/usuarios ───────────────────────────────────────────────
    /// <summary>Lista paginada de usuarios del sistema.</summary>
    [HttpGet("usuarios")]
    //[Microsoft.AspNetCore.Authorization.Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
    public async Task<IActionResult> ListarUsuarios(
        [FromQuery] ListarUsuariosRequest request,
        CancellationToken ct)
    {
        var result = await listarHandler.HandleAsync(request, ct);
        return ToPagedAction(result, request.PageNumber, request.PageSize);
    }

    // ── POST /api/auth/mantenimiento/purgar-tokens ───────────────────────────
    /// <summary>Purga lógica de tokens expirados. Solo SuperAdmin.</summary>
    [HttpPost("mantenimiento/purgar-tokens")]
    //[Microsoft.AspNetCore.Authorization.Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> PurgarTokens(CancellationToken ct)
    {
        var result = await purgarHandler.HandleAsync(ct);
        return ToAction(result);
    }

    // ── Helpers privados ─────────────────────────────────────────────────────
    private string? UserAgent =>
        HttpContext.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
}


