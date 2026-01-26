using Microsoft.Playwright;
using Scraper.Core.Interfaces;

namespace Scraper.Browser;

public class PlaywrightPageWrapper : Scraper.Core.Interfaces.IPage
{
    public readonly Microsoft.Playwright.IPage Page;

    public PlaywrightPageWrapper(Microsoft.Playwright.IPage page)
    {
        Page = page;
    }

    public string Url => Page.Url;

    public Task CloseAsync() => Page.CloseAsync();
}
