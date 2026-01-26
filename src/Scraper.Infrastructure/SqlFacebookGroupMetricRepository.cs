using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;

namespace Scraper.Infrastructure;

public class SqlFacebookGroupMetricRepository : IFacebookGroupMetricRepository
{
    private readonly ILogger<SqlFacebookGroupMetricRepository> _logger;
    private readonly string _connectionString;

    public SqlFacebookGroupMetricRepository(
        ILogger<SqlFacebookGroupMetricRepository> logger,
        string connectionString)
    {
        _logger = logger;
        _connectionString = connectionString;
    }

    public async Task SaveMetricAsync(FacebookGroupMetricDaily metric, CancellationToken cancellationToken = default)
    {
        if (metric.FechaObtencion == default)
        {
            metric.FechaObtencion = DateTime.UtcNow;
        }

        const string sql = @"
            INSERT INTO [FacebookGroupMetricDaily] 
                ([IdGrupo], [Fecha], [ClaveMetrica], [Valor], [FechaObtencion], [FechaCreacion])
            VALUES 
                (@IdGrupo, @Fecha, @ClaveMetrica, @Valor, @FechaObtencion, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@IdGrupo", metric.IdGrupo);
        command.Parameters.AddWithValue("@Fecha", metric.Fecha);
        command.Parameters.AddWithValue("@ClaveMetrica", metric.ClaveMetrica);
        command.Parameters.AddWithValue("@Valor", metric.Valor);
        command.Parameters.AddWithValue("@FechaObtencion", metric.FechaObtencion);

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Métrica guardada: IdGrupo={IdGrupo}, Clave={Clave}, Valor={Valor}, Fecha={Fecha}", 
            metric.IdGrupo, metric.ClaveMetrica, metric.Valor, metric.Fecha);
    }

    public async Task<bool> MetricExistsAsync(int idGrupo, DateTime fecha, string claveMetrica, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM [FacebookGroupMetricDaily]
            WHERE [IdGrupo] = @IdGrupo 
                AND [Fecha] = @Fecha 
                AND [ClaveMetrica] = @ClaveMetrica";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@IdGrupo", idGrupo);
        command.Parameters.AddWithValue("@Fecha", fecha.Date);
        command.Parameters.AddWithValue("@ClaveMetrica", claveMetrica);

        var count = (int)await command.ExecuteScalarAsync(cancellationToken);
        return count > 0;
    }
}
