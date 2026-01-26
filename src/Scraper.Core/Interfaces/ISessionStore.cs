namespace Scraper.Core.Interfaces;

public interface ISessionStore
{
    Task<string?> LoadStorageStateAsync(string accountId, CancellationToken cancellationToken = default);
    Task SaveStorageStateAsync(string accountId, string stateJson, CancellationToken cancellationToken = default);
    Task<bool> StorageStateExistsAsync(string accountId, CancellationToken cancellationToken = default);
}
