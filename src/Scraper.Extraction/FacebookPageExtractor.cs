using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using System.Text.Json;

namespace Scraper.Extraction;

public class FacebookPageExtractor : IPageExtractor<Dictionary<string, object>>
{
    private readonly ILogger<FacebookPageExtractor> _logger;
    private readonly IBrowserClient _browserClient;

    public FacebookPageExtractor(
        ILogger<FacebookPageExtractor> logger,
        IBrowserClient browserClient)
    {
        _logger = logger;
        _browserClient = browserClient;
    }

    public async Task<Dictionary<string, object>> ExtractAsync(IPage page, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, object>
        {
            ["url"] = page.Url,
            ["extractedAt"] = DateTime.UtcNow
        };

        try
        {
            if (page is not Browser.PlaywrightPageWrapper playwrightPage)
            {
                return result;
            }

            var content = await _browserClient.GetContentAsync(page, cancellationToken);
            if (!string.IsNullOrEmpty(content))
            {
                result["htmlLength"] = content.Length;
            }

            var title = await playwrightPage.Page.TitleAsync();
            if (!string.IsNullOrEmpty(title))
            {
                result["title"] = title;
            }

            var posts = await playwrightPage.Page.Locator("[data-pagelet='FeedUnit']").CountAsync();
            result["postCount"] = posts;

            _logger.LogInformation("Extraídos {Count} posts de la página", posts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer datos de la página");
            result["error"] = ex.Message;
        }

        return result;
    }
}
