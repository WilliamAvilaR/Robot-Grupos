namespace Scraper.Core.Interfaces;

public interface IOtpFlow
{
    Task<Guid> RequestOtpChallengeAsync(string accountId, string? correlationId, CancellationToken cancellationToken = default);
    Task<string?> WaitOtpFromDbAsync(Guid challengeId, int timeoutSeconds = 300, CancellationToken cancellationToken = default);
    Task<bool> SubmitOtpToPageAsync(IPage page, string code, CancellationToken cancellationToken = default);
}
