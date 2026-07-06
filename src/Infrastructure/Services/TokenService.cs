using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GymAffiliate.Domain.Interfaces.Services;
using GymAffiliate.Infrastructure.Configuration;
using GymAffiliate.Shared.Constants;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ClaimTypes = GymAffiliate.Shared.Constants.ClaimTypes;

namespace GymAffiliate.Infrastructure.Services;

// =============================================================================
// TokenService — vive en Infrastructure porque depende de AuthOptions.
// Implementa ITokenService (definida en Domain) para que Application
// pueda depender de la abstracción sin referenciar Infrastructure.
// Registrar como Singleton en DI (InfrastructureExtensions).
// =============================================================================
public sealed class TokenService(IOptions<AuthOptions> opts) : ITokenService
{
    private readonly JwtSettings _jwt = opts.Value.JwtSettings;

    /// <summary>
    /// Genera el Access Token (JWT) firmado con los claims del usuario.
    /// Vigencia configurada en appsettings (Auth:JwtSettings:ExpirationMinutes).
    /// </summary>
    public (string Token, string Jti, DateTime Expiry) GenerateAccessToken(
        int userId, string username, string roleCode, string roleName,
        string fullName, int? branchId)
    {
        var jti = Guid.NewGuid().ToString("N");
        var expiry = DateTime.UtcNow.AddMinutes(_jwt.ExpirationMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti,  jti),
            new(JwtRegisteredClaimNames.Sub,  userId.ToString()),
            new(JwtRegisteredClaimNames.Name, username),
            new(ClaimTypes.UserId,            userId.ToString()),
            new(ClaimTypes.RoleCode,          roleCode),
            new(ClaimTypes.FullName,          fullName),
            new(System.Security.Claims.ClaimTypes.Role, roleCode), // para [Authorize(Roles=...)]
        };

        if (branchId.HasValue)
            claims.Add(new(ClaimTypes.BranchId, branchId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiry,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), jti, expiry);
    }

    /// <summary>
    /// Genera un Refresh Token seguro (64 bytes aleatorios en Base64 URL-safe).
    /// Es opaco — se valida contra la BD, no se firma.
    /// </summary>
    public string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
               .Replace("+", "-")
               .Replace("/", "_")
               .TrimEnd('=');

    /// <summary>
    /// Hashea la contraseña con SHA-256 (compatible con el esquema actual de SystemUsers).
    /// Nota: para migrar a PBKDF2/BCrypt en el futuro, solo cambia este método.
    /// El campo PasswordSalt ya existe en SystemUsers para soportarlo.
    /// </summary>
    public string HashPassword(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLower();

    /// <summary>Parámetros de validación para el middleware JWT.</summary>
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
