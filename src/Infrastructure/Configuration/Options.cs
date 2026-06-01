using System.ComponentModel.DataAnnotations;

namespace GymAffiliate.Infrastructure.Configuration;

public class ConnectionStringOptions
{
    public const string Section = "ConnectionStrings";
    [Required(ErrorMessage = "La cadena de conexion 'DefaultConnection' es requerida.")]
    public string DefaultConnection { get; init; } = string.Empty;
}

public class AuthOptions
{
    public const string Section = "Auth";
    public bool UseJwt    { get; init; } = false;
    public bool UseApiKey { get; init; } = false;
    public JwtSettings    JwtSettings    { get; init; } = new();
    public ApiKeySettings ApiKeySettings { get; init; } = new();
}

public class JwtSettings
{
    public string Secret   { get; init; } = string.Empty;
    public string Issuer   { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    [Range(1, 1440)]
    public int ExpirationMinutes { get; init; } = 60;
}

public class ApiKeySettings
{
    public string   HeaderName { get; init; } = "X-API-Key";
    public string[] ValidKeys  { get; init; } = [];
}

public class EmailOptions
{
    public const string Section = "EmailSettings";
    public string SmtpHost    { get; init; } = string.Empty;
    public int    SmtpPort    { get; init; } = 587;
    public bool   EnableSsl   { get; init; } = true;
    public string Username    { get; init; } = string.Empty;
    public string Password    { get; init; } = string.Empty;
    public string FromAddress { get; init; } = "noreply@gymaffiliate.com";
    public string FromName    { get; init; } = "GymAffiliate";
}

public class NotificationOptions
{
    public const string Section = "NotificationSettings";
    public int DaysAheadAlert   { get; init; } = 3;
    public int MaxRetryAttempts { get; init; } = 3;
}
