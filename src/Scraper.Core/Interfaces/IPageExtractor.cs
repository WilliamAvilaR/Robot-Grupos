namespace Scraper.Core.Interfaces;

public interface IPageExtractor<T>
{
    Task<T> ExtractAsync(IPage page, CancellationToken cancellationToken = default);
}
