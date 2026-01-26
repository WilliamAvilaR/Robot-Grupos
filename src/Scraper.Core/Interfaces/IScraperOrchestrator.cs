namespace Scraper.Core.Interfaces;

public interface IScraperOrchestrator
{
    Task RunScrapeJobAsync(string accountId, IEnumerable<string> urls, CancellationToken cancellationToken = default);
    Task EnsureAuthenticatedAsync(string accountId, CancellationToken cancellationToken = default);
    Task HandleOtpFlowAsync(Guid challengeId, CancellationToken cancellationToken = default);
    Task ProcessFacebookGroupsAsync(string accountId, CancellationToken cancellationToken = default);
}
