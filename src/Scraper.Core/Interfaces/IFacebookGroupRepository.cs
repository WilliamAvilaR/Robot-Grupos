using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface IFacebookGroupRepository
{
    Task<IEnumerable<FacebookGroup>> GetActiveGroupsAsync(CancellationToken cancellationToken = default);
    Task UpdateGroupAsync(FacebookGroup group, CancellationToken cancellationToken = default);
}
