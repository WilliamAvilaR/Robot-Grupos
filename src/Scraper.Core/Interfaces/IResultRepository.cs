using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface IResultRepository
{
    Task<ScrapeResult> SaveAsync(ScrapeResult result, CancellationToken cancellationToken = default);
    Task<ScrapeResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ScrapeResult>> GetByAccountIdAsync(string accountId, CancellationToken cancellationToken = default);
}
