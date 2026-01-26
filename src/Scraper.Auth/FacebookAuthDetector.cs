using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;

namespace Scraper.Auth;

public class FacebookAuthDetector : IAuthDetector
{
    private readonly ILogger<FacebookAuthDetector> _logger;
    private readonly IBrowserClient _browserClient;

    public FacebookAuthDetector(
        ILogger<FacebookAuthDetector> logger,
        IBrowserClient browserClient)
    {
        _logger = logger;
        _browserClient = browserClient;
    }

    public async Task<AuthState> GetAuthStateAsync(IPage page, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = page.Url;
            _logger.LogInformation("Verificando estado de autenticación en: {Url}", url);

            if (page is not Browser.PlaywrightPageWrapper playwrightPage)
            {
                return AuthState.Unknown;
            }

            var pageContent = await _browserClient.GetContentAsync(page, cancellationToken);
            if (string.IsNullOrEmpty(pageContent))
            {
                return AuthState.Unknown;
            }

            if (url.Contains("login") || url.Contains("checkpoint"))
            {
                if (pageContent.Contains("two_factor") || pageContent.Contains("checkpoint") || pageContent.Contains("approvals_code"))
                {
                    _logger.LogInformation("Se requiere OTP");
                    return AuthState.NeedsOtp;
                }
                
                _logger.LogInformation("Se requiere login");
                return AuthState.NeedsLogin;
            }

            if (url.Contains("facebook.com") && !url.Contains("login") && !url.Contains("checkpoint"))
            {
                var hasLoginForm = await playwrightPage.Page.Locator("input[name='email']").CountAsync() > 0;
                var hasNewsFeed = await playwrightPage.Page.Locator("[role='main']").CountAsync() > 0 ||
                                  await playwrightPage.Page.Locator("[data-pagelet='FeedUnit']").CountAsync() > 0;

                if (hasNewsFeed)
                {
                    _logger.LogInformation("Usuario autenticado");
                    return AuthState.Authenticated;
                }

                if (hasLoginForm)
                {
                    _logger.LogInformation("Se requiere login");
                    return AuthState.NeedsLogin;
                }
            }

            if (pageContent.Contains("blocked") || pageContent.Contains("suspended"))
            {
                _logger.LogWarning("Cuenta bloqueada o suspendida");
                return AuthState.Blocked;
            }

            return AuthState.Unknown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al detectar estado de autenticación");
            return AuthState.Unknown;
        }
    }
}
