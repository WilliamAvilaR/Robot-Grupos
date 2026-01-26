using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;

namespace Scraper.Infrastructure;

public class SqlOtpChallengeRepository : IOtpChallengeRepository
{
    private readonly ILogger<SqlOtpChallengeRepository> _logger;
    private readonly string _connectionString;

    public SqlOtpChallengeRepository(
        ILogger<SqlOtpChallengeRepository> logger,
        string connectionString)
    {
        _logger = logger;
        _connectionString = connectionString;
    }

    public async Task<OtpChallenge> CreatePendingAsync(string accountId, string? correlationId, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        var challenge = new OtpChallenge
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            CorrelationId = correlationId,
            Status = OtpChallengeStatus.Pending,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        const string sql = @"
            INSERT INTO OtpChallenge (Id, AccountId, CorrelationId, Status, ExpiresAt, CreatedAt)
            VALUES (@Id, @AccountId, @CorrelationId, @Status, @ExpiresAt, @CreatedAt)";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", challenge.Id);
        command.Parameters.AddWithValue("@AccountId", challenge.AccountId);
        command.Parameters.AddWithValue("@CorrelationId", (object?)challenge.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Status", challenge.Status.ToString());
        command.Parameters.AddWithValue("@ExpiresAt", challenge.ExpiresAt);
        command.Parameters.AddWithValue("@CreatedAt", challenge.CreatedAt);

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("OtpChallenge creado: {Id} para accountId: {AccountId}", challenge.Id, accountId);

        return challenge;
    }

    public async Task<OtpChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, AccountId, CorrelationId, Status, Code, ExpiresAt, CreatedAt, UsedAt
            FROM OtpChallenge
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

    public async Task<OtpChallenge?> GetPendingByAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT TOP 1 Id, AccountId, CorrelationId, Status, Code, ExpiresAt, CreatedAt, UsedAt
            FROM OtpChallenge
            WHERE AccountId = @AccountId AND Status = 'Pending' AND ExpiresAt > GETUTCDATE()
            ORDER BY CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AccountId", accountId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapFromReader(reader);
        }

        return null;
    }

    public async Task MarkSubmittedAsync(Guid id, string code, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE OtpChallenge
            SET Status = 'Submitted', Code = @Code
            WHERE Id = @Id";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Code", code);

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("OtpChallenge marcado como Submitted: {Id}", id);
    }

    public async Task MarkUsedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE OtpChallenge
            SET Status = 'Used', UsedAt = GETUTCDATE()
            WHERE Id = @Id";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("OtpChallenge marcado como Used: {Id}", id);
    }

    public async Task MarkExpiredAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE OtpChallenge
            SET Status = 'Expired'
            WHERE Id = @Id";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("OtpChallenge marcado como Expired: {Id}", id);
    }

    private static OtpChallenge MapFromReader(SqlDataReader reader)
    {
        return new OtpChallenge
        {
            Id = reader.GetGuid(0),
            AccountId = reader.GetString(1),
            CorrelationId = reader.IsDBNull(2) ? null : reader.GetString(2),
            Status = Enum.Parse<OtpChallengeStatus>(reader.GetString(3)),
            Code = reader.IsDBNull(4) ? null : reader.GetString(4),
            ExpiresAt = reader.GetDateTime(5),
            CreatedAt = reader.GetDateTime(6),
            UsedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
        };
    }
}
