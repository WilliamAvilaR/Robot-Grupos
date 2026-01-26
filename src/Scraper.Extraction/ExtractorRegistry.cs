using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;

namespace Scraper.Extraction;

public class ExtractorRegistry : IExtractorRegistry
{
    private readonly ILogger<ExtractorRegistry> _logger;
    private readonly Dictionary<string, object> _extractors = new();

    public ExtractorRegistry(ILogger<ExtractorRegistry> logger)
    {
        _logger = logger;
    }

    public void Register<T>(string pattern, IPageExtractor<T> extractor)
    {
        _logger.LogInformation("Registrando extractor para patrón: {Pattern}", pattern);
        _extractors[pattern] = extractor;
    }

    public IPageExtractor<T>? GetExtractor<T>(string url)
    {
        foreach (var (pattern, extractor) in _extractors)
        {
            if (url.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                if (extractor is IPageExtractor<T> typedExtractor)
                {
                    return typedExtractor;
                }
            }
        }

        return null;
    }
}
