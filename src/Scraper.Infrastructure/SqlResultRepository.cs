using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Scraper.Infrastructure;

public class SqlResultRepository : IResultRepository
{
    private readonly ILogger<SqlResultRepository> _logger;
    private readonly string _connectionString;

    public SqlResultRepository(
        ILogger<SqlResultRepository> logger,
        string connectionString)
    {
        _logger = logger;
        _connectionString = connectionString;
    }

    public async Task<ScrapeResult> SaveAsync(ScrapeResult result, CancellationToken cancellationToken = default)
    {
        if (result.Id == Guid.Empty)
        {
            result.Id = Guid.NewGuid();
        }

        if (result.CapturedAt == default)
        {
            result.CapturedAt = DateTime.UtcNow;
        }

        if (string.IsNullOrEmpty(result.ContentHash) && !string.IsNullOrEmpty(result.PayloadJson))
        {
            result.ContentHash = ComputeHash(result.PayloadJson);
        }

        const string sql = @"
            INSERT INTO ScrapeResult (Id, AccountId, Url, CapturedAt, PayloadJson, ContentHash, HtmlSnapshotPath)
            VALUES (@Id, @AccountId, @Url, @CapturedAt, @PayloadJson, @ContentHash, @HtmlSnapshotPath)";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", result.Id);
        command.Parameters.AddWithValue("@AccountId", result.AccountId);
        command.Parameters.AddWithValue("@Url", result.Url);
        command.Parameters.AddWithValue("@CapturedAt", result.CapturedAt);
        command.Parameters.AddWithValue("@PayloadJson", (object?)result.PayloadJson ?? DBNull.Value);
        command.Parameters.AddWithValue("@ContentHash", (object?)result.ContentHash ?? DBNull.Value);
        command.Parameters.AddWithValue("@HtmlSnapshotPath", (object?)result.HtmlSnapshotPath ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("ScrapeResult guardado: {Id} para accountId: {AccountId}", result.Id, result.AccountId);

        return result;
    }

    public async Task<ScrapeResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, AccountId, Url, CapturedAt, PayloadJson, ContentHash, HtmlSnapshotPath
            FROM ScrapeResult
            WHERE Id = @Id";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapFromReader(reader);
        }

        return null;
    }

    public async Task<IEnumerable<ScrapeResult>> GetByAccountIdAsync(string accountId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, AccountId, Url, CapturedAt, PayloadJson, ContentHash, HtmlSnapshotPath
            FROM ScrapeResult
            WHERE AccountId = @AccountId
            ORDER BY CapturedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AccountId", accountId);

        var results = new List<ScrapeResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapFromReader(reader));
        }

        return results;
    }

    private static ScrapeResult MapFromReader(SqlDataReader reader)
    {
        return new ScrapeResult
        {
            Id = reader.GetGuid(0),
            AccountId = reader.GetString(1),
            Url = reader.GetString(2),
            CapturedAt = reader.GetDateTime(3),
            PayloadJson = reader.IsDBNull(4) ? null : reader.GetString(4),
            ContentHash = reader.IsDBNull(5) ? null : reader.GetString(5),
            HtmlSnapshotPath = reader.IsDBNull(6) ? null : reader.GetString(6)
        };
    }

    private static string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hashBytes);
    }
}
