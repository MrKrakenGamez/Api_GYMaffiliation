using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GymAffiliate.Infrastructure.Configuration;

namespace GymAffiliate.Infrastructure.Persistence.Dapper.Context;

/// <summary>
/// Punto único de acceso a conexiones SQL.
/// Toda operación de base de datos pasa exclusivamente por aquí.
/// Inyectado como Scoped: una conexión por request HTTP.
/// </summary>
public interface IDapperContext
{
    /// <summary>Crea una conexión SQL sin abrir (para uso manual con using).</summary>
    IDbConnection CreateConnection();

    /// <summary>
    /// Ejecuta una operación recibiendo una conexión ya abierta.
    /// Abre, ejecuta y cierra la conexión automáticamente.
    /// </summary>
    Task<T> ExecuteAsync<T>(
        Func<IDbConnection, Task<T>> operation,
        CancellationToken ct = default);
}

public sealed class DapperContext : IDapperContext
{
    private readonly string _connectionString;
    private readonly ILogger<DapperContext> _log;

    public DapperContext(
        IOptions<ConnectionStringOptions> opts,
        ILogger<DapperContext> log)
    {
        _connectionString = opts.Value.DefaultConnection;
        _log              = log;

        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException(
                "ConnectionString 'DefaultConnection' no está configurada. " +
                "Revisa appsettings.json o User Secrets.");
    }

    /// <inheritdoc />
    public IDbConnection CreateConnection() =>
        new SqlConnection(_connectionString);

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(
        Func<IDbConnection, Task<T>> operation,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // IMPORTANTE: usar 'using' sincrono, NO 'await using'.
        // 'await using' cierra la conexion antes de que el GridReader termine de leer,
        // causando 'The reader has been disposed'.
        using var conn = new SqlConnection(_connectionString);

        try
        {
            await conn.OpenAsync(ct);
            return await operation(conn);
        }
        catch (SqlException ex)
        {
            _log.LogError(ex,
                "SqlException al ejecutar operación. Number={Number} Severity={Severity} State={State}",
                ex.Number, ex.Class, ex.State);
            throw;
        }
        catch (OperationCanceledException)
        {
            _log.LogWarning("Operación de base de datos cancelada por el cliente.");
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error inesperado en operación de base de datos.");
            throw;
        }
    }
}
