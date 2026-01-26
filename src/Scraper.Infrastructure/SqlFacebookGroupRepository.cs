using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;

namespace Scraper.Infrastructure;

public class SqlFacebookGroupRepository : IFacebookGroupRepository
{
    private readonly ILogger<SqlFacebookGroupRepository> _logger;
    private readonly string _connectionString;

    public SqlFacebookGroupRepository(
        ILogger<SqlFacebookGroupRepository> logger,
        string connectionString)
    {
        _logger = logger;
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<FacebookGroup>> GetActiveGroupsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT TOP (1000) 
                [IdGrupo],
                [IdUsuario],
                [FacebookGroupId],
                [UrlOriginal],
                [Nombre],
                [UrlImagen],
                [Activo],
                [FechaUltimaSincronizacion],
                [FechaCreacion]
            FROM [DataColorApp].[dbo].[FacebookGroup]
            WHERE [Activo] = 1 OR [Activo] IS NULL
            ORDER BY [IdGrupo]";

        var groups = new List<FacebookGroup>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            groups.Add(MapFromReader(reader));
        }

        _logger.LogInformation("Se encontraron {Count} grupos activos", groups.Count);
        return groups;
    }

    public async Task UpdateGroupAsync(FacebookGroup group, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE [FacebookGroup]
            SET [Nombre] = @Nombre,
                [UrlImagen] = @UrlImagen,
                [FechaCreacionFacebook] = @FechaCreacionFacebook,
                [FechaUltimaSincronizacion] = GETUTCDATE()
            WHERE [IdGrupo] = @IdGrupo";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@IdGrupo", group.IdGrupo);
        command.Parameters.AddWithValue("@Nombre", (object?)group.Nombre ?? DBNull.Value);
        command.Parameters.AddWithValue("@UrlImagen", (object?)group.UrlImagen ?? DBNull.Value);
        command.Parameters.AddWithValue("@FechaCreacionFacebook", (object?)group.FechaCreacionFacebook ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Grupo actualizado: IdGrupo={IdGrupo}, Nombre={Nombre}, FechaCreacionFacebook={Fecha}", 
            group.IdGrupo, group.Nombre, group.FechaCreacionFacebook);
    }

    private static FacebookGroup MapFromReader(SqlDataReader reader)
    {
        return new FacebookGroup
        {
            IdGrupo = reader.GetInt32(0),
            IdUsuario = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            FacebookGroupId = reader.IsDBNull(2) ? null : reader.GetString(2),
            UrlOriginal = reader.GetString(3),
            Nombre = reader.IsDBNull(4) ? null : reader.GetString(4),
            UrlImagen = reader.IsDBNull(5) ? null : reader.GetString(5),
            Activo = reader.IsDBNull(6) ? null : reader.GetBoolean(6),
            FechaUltimaSincronizacion = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
            FechaCreacion = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
        };
    }
}
