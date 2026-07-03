using GymAffiliate.Shared.Result;

namespace GymAffiliate.Shared.Errors;

public static class ErrorCodes
{
    public const string AfiliadoDuplicado        = "AF_001";
    public const string AfiliadoDatosFaltantes   = "AF_002";
    public const string AfiliadoNoEncontrado     = "AF_004";
    public const string MembresiaVencida         = "MB_005";
    public const string PagoDuplicado            = "PA_006";
    public const string NoAutorizado             = "AU_007";
    public const string SucursalNoEncontrada     = "SU_008";
    public const string NotificacionEnvioError   = "NT_009";
    public const string MembresiaNoEncontrada    = "MB_010";
    public const string OperacionInvalida        = "SY_902";
    public const string PagoMetodoNoEncontrado   = "PA_013";
    public const string MembresiaWrongBranch     = "MB_014";
    public const string AccesoSuspendido         = "AC_015";
    public const string AfiliadoYaEliminado      = "AF_016";
    public const string PagoMontoInvalido        = "PA_019";
    public const string PagoYaCancelado          = "PA_020";
    public const string ConexionBD               = "SY_003";
    public const string ErrorInesperado          = "SY_901";
    public const string ErrorValidacion          = "VAL_001";
    //auth errors
    public const string CredencialesInvalidas    = "CI_021";
    public const string UsuarioInactivo          = "UI_022";
    public const string UsuarioNoEncontrado      = "UNE_023";
    public const string UsuarioDuplicado         = "UD_024";
}

public static class SpErrorMapper
{
    private static readonly Dictionary<int, (string Code, int HttpStatus)> Map = new()
    {
        { 1,  (ErrorCodes.AfiliadoDuplicado,       409) },
        { 2,  (ErrorCodes.AfiliadoDatosFaltantes,  422) },
        { 3,  (ErrorCodes.ConexionBD,              503) },
        { 4,  (ErrorCodes.AfiliadoNoEncontrado,    404) },
        { 5,  (ErrorCodes.MembresiaVencida,        403) },
        { 6,  (ErrorCodes.PagoDuplicado,           409) },
        { 7,  (ErrorCodes.NoAutorizado,            401) },
        { 8,  (ErrorCodes.SucursalNoEncontrada,    404) },
        { 9,  (ErrorCodes.NotificacionEnvioError,  500) },
        { 10, (ErrorCodes.MembresiaNoEncontrada,   404) },
        { 11, (ErrorCodes.OperacionInvalida,       400) },
        { 12, (ErrorCodes.MembresiaNoEncontrada,   404) },
        { 13, (ErrorCodes.PagoMetodoNoEncontrado,  404) },
        { 14, (ErrorCodes.MembresiaWrongBranch,    403) },
        { 15, (ErrorCodes.AccesoSuspendido,        403) },
        { 16, (ErrorCodes.AfiliadoYaEliminado,     400) },
        { 19, (ErrorCodes.PagoMontoInvalido,       422) },
        { 20, (ErrorCodes.PagoYaCancelado,         400) },
        { 21, (ErrorCodes.CredencialesInvalidas,   401) },
        { 22, (ErrorCodes.UsuarioInactivo,         403) },
        { 23, (ErrorCodes.UsuarioNoEncontrado,    404) },
        { 24, (ErrorCodes.UsuarioDuplicado,       409) },
    };
    public static ResultError ToResultError(int errorId, string spMessage)
    {
        if (Map.TryGetValue(errorId, out var m))
            return new ResultError(m.Code, spMessage, m.HttpStatus);
        return new ResultError(ErrorCodes.ErrorInesperado, spMessage, 500);
    }
}
