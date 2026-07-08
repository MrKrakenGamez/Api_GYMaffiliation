using FluentValidation;
using GymAffiliate.Application.DTOs.Requests;
using GymAffiliate.Application.DTOs.Responses;
using GymAffiliate.Domain.Interfaces.Repositories;
using GymAffiliate.Shared.Errors;
using GymAffiliate.Shared.Result;
using Microsoft.Extensions.Logging;
using GymAffiliate.Domain.Interfaces.Services;
using LoginRequest = GymAffiliate.Application.DTOs.Requests.LoginRequest;


namespace GymAffiliate.Application.UseCases.Auth;

// =============================================================================
// LoginHandler
// =============================================================================
public sealed class LoginHandler(
     IAuthRepository repo,
     IValidator<LoginRequest> validator,
     ITokenService itokenService,
     ILogger<LoginHandler> log
    )
{
    public async Task<Result<LoginResponse>> HandleAsync(
        LoginRequest request,string? ip, string? userAgent,
        CancellationToken ct=default
        )
    {
        // 1. Validar input
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Result<LoginResponse>.Failure(
                new ResultError(ErrorCodes.ErrorValidacion, "Errores de validación.", 422, errors));
        }
        // 2. Hashear contraseña y validar en BD
        //var passwordHash = itokenService.HashPassword(request.Password);
        var passwordHash = request.Password;
        var refreshToken = itokenService.GenerateRefreshToken();   // ← genera ANTES
        var refreshExpiry = DateTime.UtcNow.AddDays(30);

        var loginResult = await repo.LoginAsync(request.Username, passwordHash,refreshToken, ip, userAgent, ct);

        if (loginResult.IsFailure)
            return loginResult.Map(_ => (LoginResponse)null!);

        var user = loginResult.Value;

        // 3. Generar Access Token
        //var (accessToken, jti, accessExpiry) = itokenService.GenerateAccessToken(
        //    user.UserId, user.Username, user.RoleCode, user.RoleName, user.FullName, user.BranchId
        //    );
        var (accessToken, jti, accessExpiry) = itokenService.GenerateAccessToken(
        user.UserId, user.Username, user.RoleCode, user.RoleName, user.FullName, user.BranchId);

        // 4. Guardar Refresh Token en BD
        log.LogInformation("Login exitoso UserId={UserId} Role={Role}", user.UserId, user.RoleCode);

        return Result<LoginResponse>.Success(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiry = accessExpiry,
            RefreshTokenExpiry = refreshExpiry,
            Usuario = new UsuarioInfoResponse
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                RoleCode = user.RoleCode,
                RoleName = user.RoleName,
                BranchId = user.BranchId,
            }
        }
        );

    }
}

// =============================================================================
// RefreshTokenHandler
// =============================================================================

public sealed class RefreshTokenHandler(
    IAuthRepository repo,
    IValidator<RefreshTokenRequest> validator,
    ITokenService itokenService,
    ILogger<RefreshTokenHandler> log
    )
{
    public async Task<Result<RefreshTokenResponse>> HandleAsync(
        RefreshTokenRequest request, string? ip, string? userAgent, CancellationToken ct = default
        )
    {
        var validation = await validator.ValidateAsync(request,ct);
        if (!validation.IsValid)
        {
            var erros = validation.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            return Result<RefreshTokenResponse>.Failure(
                 new ResultError(ErrorCodes.ErrorValidacion, "Errores de Validación.", 422, erros));
        }
        // SP rota el token y retorna datos del usuario
        var result = await repo.RefreshTokenAsync(request.RefreshToken, ip, userAgent, ct);
        if (result.IsFailure)
            return result.Map(_ => (RefreshTokenResponse)null!);

        var data = result.Value;

        // Generar nuevo Access Token con los datos del usuario
        var (accessToken, _, accessExpiry) = itokenService.GenerateAccessToken(
            data.UserId, data.Username, data.RoleCode, data.RoleName, data.FullName, data.BranchId
            );

        log.LogInformation("Refresh exitoso UserId={UserId}", data.UserId);

        return Result<RefreshTokenResponse>.Success(
            new RefreshTokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = data.NewRefreshToken,
                AccessTokenExpiry = accessExpiry,
                RefreshTokenExpiry = data.RefreshTokenExpiry,
            }
            );
    }
}


// =============================================================================
// LogoutHandler
// =============================================================================
public sealed class LogoutHandler(IAuthRepository repo, ILogger<LogoutHandler> log)
{
    public async Task<Result> HandleAsync(
        string? refreshToken, int? userId, bool logoutAll,
        CancellationToken ct = default)
    {
        // logoutAll = true → revocar todas las sesiones del usuario (por userId)
        var rtParam = logoutAll ? null : refreshToken;
        var uidParam = logoutAll ? null : userId;

        var result = await repo.LogoutAsync(rtParam, uidParam, ct);

        if (result.IsSuccess)
            log.LogInformation("Logout UserId={UserId} LogoutAll={All}", userId, logoutAll);

        return result;
    }
}

// =============================================================================
// CrearUsuarioHandler
// =============================================================================
public sealed class CrearUsuarioHandler(
    IAuthRepository repo,
    IValidator<CrearUsuarioRequest> validator,
    ILogger<CrearUsuarioHandler> log,
    ITokenService itokenService
    )
{
    public async Task<Result<UsuarioSistemaResponse>> HandleAsync(
        CrearUsuarioRequest request, int operatedBy,
        CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Result<UsuarioSistemaResponse>.Failure(
                new ResultError(ErrorCodes.ErrorValidacion, "Errores de validación.", 422, errors));
        }

        //var passwordHash = itokenService.HashPassword(request.Password);
        var passwordHash = request.Password;


        var result = await repo.CrearUsuarioAsync(new CrearUsuarioParams(
            request.Username, passwordHash, request.FullName,
            request.Email, request.RoleId, request.BranchId, operatedBy), ct);

        if (result.IsFailure)
            return result.Map(_ => (UsuarioSistemaResponse)null!);

        var u = result.Value;
        log.LogInformation("Usuario creado Id={UserId} por OperatedBy={Op}", u.UserId, operatedBy);

        return Result<UsuarioSistemaResponse>.Success(new UsuarioSistemaResponse
        {
            UserId = u.UserId,
            Username = u.Username,
            FullName = u.FullName,
            Email = u.Email,
            RoleCode = u.RoleCode,
            RoleName = u.RoleName,
            BranchId = u.BranchId,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
        });
    }
}

// =============================================================================
// DarDeBajaUsuarioHandler
// =============================================================================
public sealed class DarDeBajaUsuarioHandler(
    IAuthRepository repo,
    IValidator<DarDeBajaRequest> validator,
    ILogger<DarDeBajaUsuarioHandler> log)
{
    public async Task<Result> HandleAsync(
        DarDeBajaRequest request, int operatedBy,
        CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Result.Failure(
                new ResultError(ErrorCodes.ErrorValidacion, "Errores de validación.", 422, errors));
        }

        return await repo.DarDeBajaAsync(request.UserId, request.Reason, operatedBy, ct);
    }
}

// =============================================================================
// ObtenerUsuarioHandler
// =============================================================================
public sealed class ObtenerUsuarioHandler(IAuthRepository repo)
{
    public async Task<Result<UsuarioSistemaResponse?>> HandleAsync(
        int? userId, string? username, CancellationToken ct = default)
    {
        var result = await repo.ObtenerUsuarioAsync(userId, username, ct);
        if (result.IsFailure) return result.Map(_ => (UsuarioSistemaResponse?)null);
        if (result.Value is null) return Result<UsuarioSistemaResponse?>.Success(null);

        var u = result.Value;
        return Result<UsuarioSistemaResponse?>.Success(new UsuarioSistemaResponse
        {
            UserId = u.UserId,
            Username = u.Username,
            FullName = u.FullName,
            Email = u.Email,
            RoleCode = u.RoleCode,
            RoleName = u.RoleName,
            BranchId = u.BranchId,
            BranchName = u.BranchName,
            IsActive = u.IsActive,
            LastLogin = u.LastLogin,
            CreatedAt = u.CreatedAt,
            DeactivatedAt = u.DeactivatedAt,
            DeactivationReason = u.DeactivationReason,
        });
    }
}

// =============================================================================
// ListarUsuariosHandler
// =============================================================================
public sealed class ListarUsuariosHandler(IAuthRepository repo)
{
    public async Task<Result<(IEnumerable<UsuarioSistemaListaResponse> Items, int Total)>> HandleAsync(
        ListarUsuariosRequest request, CancellationToken ct = default)
    {
        var result = await repo.ListarUsuariosAsync(
            request.RoleId, request.BranchId,
            request.PageNumber, request.PageSize, ct);

        if (result.IsFailure)
            return result.Map(_ => ((IEnumerable<UsuarioSistemaListaResponse>)[], 0));

        var (items, total) = result.Value;

        var mapped = items.Select(u => new UsuarioSistemaListaResponse
        {
            UserId = u.UserId,
            Username = u.Username,
            FullName = u.FullName,
            Email = u.Email,
            RoleCode = u.RoleCode,
            RoleName = u.RoleName,
            BranchName = u.BranchName,
            IsActive = u.IsActive,
            LastLogin = u.LastLogin,
            CreatedAt = u.CreatedAt,
        });

        return Result<(IEnumerable<UsuarioSistemaListaResponse>, int)>.Success((mapped, total));
    }
}

// =============================================================================
// PurgarTokensHandler
// =============================================================================
public sealed class PurgarTokensHandler(IAuthRepository repo, ILogger<PurgarTokensHandler> log)
{
    public async Task<Result<PurgaTokensResponse>> HandleAsync(CancellationToken ct = default)
    {
        var result = await repo.PurgarTokensAsync(ct);
        if (result.IsFailure)
            return result.Map(_ => (PurgaTokensResponse)null!);

        var r = result.Value;
        log.LogInformation("Purga completada: Refresh={R} Access={A}", r.PurgedRefreshTokens, r.PurgedAccessTokens);

        return Result<PurgaTokensResponse>.Success(new PurgaTokensResponse
        {
            PurgedRefreshTokens = r.PurgedRefreshTokens,
            PurgedAccessTokens = r.PurgedAccessTokens,
            ExecutedAt = r.ExecutedAt,
            CutoffDate = r.CutoffDate,
        });
    }
}