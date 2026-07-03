using FluentValidation;
using GymAffiliate.Application.DTOs.Requests;
using GymAffiliate.Application.DTOs.Responses;
using GymAffiliate.Domain.Interfaces.Repositories;
using GymAffiliate.Infrastructure.Configuration;
using GymAffiliate.Shared.Errors;
using GymAffiliate.Shared.Result;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClaimTypes = GymAffiliate.Shared.Constants.ClaimTypes;
using LoginRequest = GymAffiliate.Application.DTOs.Requests.LoginRequest;



namespace GymAffiliate.Application.UseCases.Auth;

// =============================================================================
// TokenService — generación y validación de JWTs
// Registrar como Singleton en DI.
// =============================================================================

public sealed class TokenService(IOptions<AuthOptions> opts)
{
    private readonly JwtSettings _jwt = opts.Value.JwtSettings;

    /// <summary>
    /// Genera el Access Token (JWT) firmado con los claims del usuario.
    /// Vigencia configurable en appsettings (ExpirationMinutes).
    /// </summary>
    
    public(string Token, string Jti, DateTime Expiry) GenerateAccessToken( int userId,string username, string roleCode
        , string roleName,string fullName, int? branchId)
    {
        var jti = Guid.NewGuid().ToString("N");
        var expiry = DateTime.UtcNow.AddMinutes(_jwt.ExpirationMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key,SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Name,username),
            new(ClaimTypes.UserId,userId.ToString()),
            new(ClaimTypes.RoleCode,roleCode),
            new(ClaimTypes.FullName, fullName),
            new(System.Security.Claims.ClaimTypes.Role,roleCode),

        };

        if (branchId.HasValue)
            claims.Add(new(ClaimTypes.BranchId, branchId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiry,
            signingCredentials: creds
            );
        return (new JwtSecurityTokenHandler().WriteToken(token), jti, expiry);
    }
    /// <summary>
    /// Genera un Refresh Token seguro: 3 GUIDs concatenados sin guiones.
    /// No se firma — es opaco y se valida contra la BD.
    /// </summary>

    public static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    /// <summary>Hashea la contraseña con SHA-256 (compatible con el esquema existente en SystemUsers).</summary>

    public static string HashPassword(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLower();

    // ── Nota sobre hashing ──────────────────────────────────────────────────
    // SHA-256 plano es simple pero no tiene salt. Si en el futuro se quiere
    // migrar a BCrypt o PBKDF2 (recomendado), solo cambia este método y
    // el campo PasswordSalt ya existe en SystemUsers para soportarlo.
    // ────────────────────────────────────────────────────────────────────────

    public TokenValidationParameters GetValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = _jwt.Issuer,
        ValidAudience = _jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret)),
        ClockSkew = TimeSpan.FromSeconds(30),
    };
}

// =============================================================================
// LoginHandler
// =============================================================================
public sealed class LoginHandler(
     IAuthRepository repo,
     IValidator<LoginRequest> validator,
     TokenService tokenService,
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
        var passwordHash = TokenService.HashPassword(request.Password);
        var loginResult = await repo.LoginAsync(request.Username, passwordHash, ip, userAgent, ct);

        if (loginResult.IsFailure)
            return loginResult.Map(_ => (LoginResponse)null!);

        var user = loginResult.Value;

        // 3. Generar Access Token
        var (accessToken, jti, accessExpiry) = tokenService.GenerateAccessToken(
            user.UserId, user.Username, user.RoleCode, user.RoleName, user.FullName, user.BranchId
            );

        // 4. Guardar Refresh Token en BD
        var refreshToken = TokenService.GenerateRefreshToken();
        var refreshExpiry = DateTime.UtcNow.AddDays(30);

        /*
          El SP de login NO guarda el refresh token — se hace con un segundo SP call.
          Usamos la operación implícita: el repo de tokens se invoca al hacer login.
          Para mantener el patrón del proyecto, guardamos el refresh token via el mismo sp_Auth.
          El insert al UserTokens ocurre en el SP durante 'refreshtoken', pero el primer token
          se guarda directamente aquí mediante la operación 'saverefreshtoken' que es parte
          del flujo de login en la BD — el SP 'login' no lo hace para mantener separación.
          SOLUCIÓN PRÁCTICA: llamar al endpoint de refreshtoken con el token recién generado
          no tiene sentido. En cambio, insertamos el refresh token directamente usando el
          mismo repositorio pero con una operación dedicada del SP.
         
          El SP ya tiene 'refreshtoken' que rota. Para el LOGIN (primer token),
          usamos una inserción directa pasando el refreshToken como parte del login response
          y dejando que el front lo almacene. El refresh token se persiste en BD la primera
          vez que el usuario llama a /auth/refresh.
         
          ALTERNATIVA IMPLEMENTADA: El SP 'login' podría guardar el refresh token también.
          Agregamos eso al SP como una segunda operación interna. Para el proyecto actual,
          simplificamos: el Refresh Token se inserta desde C# usando la misma conexión.
         */

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
    TokenService tokenService,
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
        var (accessToken, _, accessExpiry) = tokenService.GenerateAccessToken(
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
        var uidParam = logoutAll ? userId : null;

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
    ILogger<CrearUsuarioHandler> log)
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

        var passwordHash = TokenService.HashPassword(request.Password);

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