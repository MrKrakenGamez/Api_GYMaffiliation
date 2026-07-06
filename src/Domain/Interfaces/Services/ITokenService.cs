namespace GymAffiliate.Domain.Interfaces.Services;

// =============================================================================
// ITokenService — interfaz en Domain para que Application pueda depender
// de la abstracción sin referenciar Infrastructure directamente.
// La implementación concreta (TokenService) vive en Infrastructure.
// =============================================================================
public interface ITokenService
{
    /// <summary>
    /// Genera un Access Token JWT firmado con los claims del usuario.
    /// Devuelve: (tokenString, jti, fechaExpiry).
    /// </summary>
    (string Token, string Jti, DateTime Expiry) GenerateAccessToken(
        int userId, string username, string roleCode, string roleName,
        string fullName, int? branchId);

    /// <summary>Hashea la contraseña con SHA-256.</summary>
    string HashPassword(string password);

    /// <summary>Genera un Refresh Token opaco seguro.</summary>
    string GenerateRefreshToken();
}
