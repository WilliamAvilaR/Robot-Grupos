using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface ILoginFlow
{
    Task<bool> PerformLoginAsync(IPage page, FacebookCredentials credentials, CancellationToken cancellationToken = default);
}
