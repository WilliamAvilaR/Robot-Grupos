using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface IAuthDetector
{
    Task<AuthState> GetAuthStateAsync(IPage page, CancellationToken cancellationToken = default);
}
