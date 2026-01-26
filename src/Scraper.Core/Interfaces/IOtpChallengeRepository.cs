using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface IOtpChallengeRepository
{
    Task<OtpChallenge> CreatePendingAsync(string accountId, string? correlationId, DateTime expiresAt, CancellationToken cancellationToken = default);
    Task<OtpChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OtpChallenge?> GetPendingByAccountAsync(string accountId, CancellationToken cancellationToken = default);
    Task MarkSubmittedAsync(Guid id, string code, CancellationToken cancellationToken = default);
    Task MarkUsedAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkExpiredAsync(Guid id, CancellationToken cancellationToken = default);
}
