namespace Scraper.Core.Interfaces;

public interface IExtractorRegistry
{
    void Register<T>(string pattern, IPageExtractor<T> extractor);
    IPageExtractor<T>? GetExtractor<T>(string url);
}
