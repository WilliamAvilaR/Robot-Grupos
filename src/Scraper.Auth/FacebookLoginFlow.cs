using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;

namespace Scraper.Auth;

public class FacebookLoginFlow : ILoginFlow
{
    private readonly ILogger<FacebookLoginFlow> _logger;
    private readonly IBrowserClient _browserClient;

    public FacebookLoginFlow(
        ILogger<FacebookLoginFlow> logger,
        IBrowserClient browserClient)
    {
        _logger = logger;
        _browserClient = browserClient;
    }

    public async Task<bool> PerformLoginAsync(IPage page, FacebookCredentials credentials, CancellationToken cancellationToken = default)
    {
        try
        {
            if (page is not Browser.PlaywrightPageWrapper playwrightPage)
            {
                throw new ArgumentException("Page debe ser una instancia de PlaywrightPage", nameof(page));
            }

            _logger.LogInformation("Iniciando proceso de login para: {Email}", credentials.Email);

            await playwrightPage.Page.WaitForSelectorAsync("input[name='email']", new Microsoft.Playwright.PageWaitForSelectorOptions
            {
                Timeout = 10000
            });

            await playwrightPage.Page.FillAsync("input[name='email']", credentials.Email);
            await playwrightPage.Page.FillAsync("input[name='pass']", credentials.Password);
            
            await playwrightPage.Page.ClickAsync("button[type='submit']");
            
            await playwrightPage.Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
            
            await Task.Delay(2000, cancellationToken);

            var currentUrl = playwrightPage.Page.Url;
            _logger.LogInformation("URL después del login: {Url}", currentUrl);

            if (currentUrl.Contains("checkpoint") || currentUrl.Contains("two_factor"))
            {
                _logger.LogInformation("Se requiere verificación adicional (OTP)");
                return false;
            }

            if (currentUrl.Contains("facebook.com") && !currentUrl.Contains("login"))
            {
                _logger.LogInformation("Login exitoso");
                return true;
            }

            _logger.LogWarning("Login fallido o estado desconocido");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el proceso de login");
            return false;
        }
    }
}
