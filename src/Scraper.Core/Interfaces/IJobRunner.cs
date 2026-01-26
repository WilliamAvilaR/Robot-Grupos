using Microsoft.Extensions.Hosting;

namespace Scraper.Core.Interfaces;

public interface IJobRunner : IHostedService
{
    Task RunJobAsync(string accountId, IEnumerable<string> urls, CancellationToken cancellationToken = default);
}
