using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scraper.Core.Interfaces;

namespace Scraper.Auth;

public class FileSessionStore : ISessionStore
{
    private readonly ILogger<FileSessionStore> _logger;
    private readonly SessionStoreOptions _options;

    public FileSessionStore(
        ILogger<FileSessionStore> logger,
        IOptions<SessionStoreOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task<string?> LoadStorageStateAsync(string accountId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_options.StorageStatePath, accountId, "storageState.json");
        
        if (!File.Exists(path))
        {
            _logger.LogInformation("No existe storage state para accountId: {AccountId}", accountId);
            return Task.FromResult<string?>(null);
        }

        _logger.LogInformation("Cargando storage state desde: {Path}", path);
        var content = File.ReadAllText(path);
        return Task.FromResult<string?>(content);
    }

    public Task SaveStorageStateAsync(string accountId, string stateJson, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(_options.StorageStatePath, accountId);
        Directory.CreateDirectory(directory);
        
        var path = Path.Combine(directory, "storageState.json");
        _logger.LogInformation("Guardando storage state en: {Path}", path);
        
        File.WriteAllText(path, stateJson);
        return Task.CompletedTask;
    }

    public Task<bool> StorageStateExistsAsync(string accountId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_options.StorageStatePath, accountId, "storageState.json");
        return Task.FromResult(File.Exists(path));
    }
}

public class SessionStoreOptions
{
    public string StorageStatePath { get; set; } = @"C:\ScraperData\state";
}
