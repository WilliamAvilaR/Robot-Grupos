using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface IBrowserClient : IDisposable
{
    Task CreateContextAsync(string accountId, CancellationToken cancellationToken = default);
    Task<IPage> NewPageAsync(string accountId, CancellationToken cancellationToken = default);
    Task GoToAsync(IPage page, string url, CancellationToken cancellationToken = default);
    Task<NavigationResult> GoToAsyncWithTiming(IPage page, string url, CancellationToken cancellationToken = default);
    Task WaitForSelectorAsync(IPage page, string selector, int timeoutMs = 30000, CancellationToken cancellationToken = default);
    Task<T?> EvaluateAsync<T>(IPage page, string script, CancellationToken cancellationToken = default);
    Task<byte[]?> ScreenshotAsync(IPage page, CancellationToken cancellationToken = default);
    Task<string?> GetContentAsync(IPage page, CancellationToken cancellationToken = default);
    Task SaveStorageStateAsync(string accountId, CancellationToken cancellationToken = default);
}

public interface IPage
{
    string Url { get; }
    Task CloseAsync();
}
