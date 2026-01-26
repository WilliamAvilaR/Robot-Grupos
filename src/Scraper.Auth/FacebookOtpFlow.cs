using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;

namespace Scraper.Auth;

public class FacebookOtpFlow : IOtpFlow
{
    private readonly ILogger<FacebookOtpFlow> _logger;
    private readonly IBrowserClient _browserClient;
    private readonly IOtpChallengeRepository _otpRepository;

    public FacebookOtpFlow(
        ILogger<FacebookOtpFlow> logger,
        IBrowserClient browserClient,
        IOtpChallengeRepository otpRepository)
    {
        _logger = logger;
        _browserClient = browserClient;
        _otpRepository = otpRepository;
    }

    public async Task<Guid> RequestOtpChallengeAsync(string accountId, string? correlationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creando desafío OTP para accountId: {AccountId}", accountId);
        
        var challenge = await _otpRepository.CreatePendingAsync(
            accountId,
            correlationId,
            DateTime.UtcNow.AddMinutes(10),
            cancellationToken);

        _logger.LogInformation("Desafío OTP creado: {ChallengeId}. Esperando código manual...", challenge.Id);
        return challenge.Id;
    }

    public async Task<string?> WaitOtpFromDbAsync(Guid challengeId, int timeoutSeconds = 300, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Esperando código OTP para challengeId: {ChallengeId} (timeout: {Timeout}s)", challengeId, timeoutSeconds);

        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        while (DateTime.UtcNow - startTime < timeout)
        {
            var challenge = await _otpRepository.GetByIdAsync(challengeId, cancellationToken);
            
            if (challenge == null)
            {
                _logger.LogWarning("Challenge no encontrado: {ChallengeId}", challengeId);
                return null;
            }

            if (challenge.Status == Core.Models.OtpChallengeStatus.Submitted && !string.IsNullOrEmpty(challenge.Code))
            {
                _logger.LogInformation("Código OTP recibido: {Code}", challenge.Code);
                await _otpRepository.MarkUsedAsync(challengeId, cancellationToken);
                return challenge.Code;
            }

            if (challenge.Status == Core.Models.OtpChallengeStatus.Expired)
            {
                _logger.LogWarning("Challenge expirado: {ChallengeId}", challengeId);
                return null;
            }

            await Task.Delay(2000, cancellationToken);
        }

        _logger.LogWarning("Timeout esperando código OTP para challengeId: {ChallengeId}", challengeId);
        await _otpRepository.MarkExpiredAsync(challengeId, cancellationToken);
        return null;
    }

    public async Task<bool> SubmitOtpToPageAsync(IPage page, string code, CancellationToken cancellationToken = default)
    {
        try
        {
            if (page is not Browser.PlaywrightPageWrapper playwrightPage)
            {
                throw new ArgumentException("Page debe ser una instancia de PlaywrightPage", nameof(page));
            }

            _logger.LogInformation("Enviando código OTP a la página");

            var codeInputSelector = "input[name='approvals_code'], input[name='checkpoint_code'], input[type='text'][placeholder*='código']";
            
            await playwrightPage.Page.WaitForSelectorAsync(codeInputSelector, new Microsoft.Playwright.PageWaitForSelectorOptions
            {
                Timeout = 10000
            });

            await playwrightPage.Page.FillAsync(codeInputSelector, code);
            
            var submitButton = await playwrightPage.Page.QuerySelectorAsync("button[type='submit'], button[name='submit[Continue]']");
            if (submitButton != null)
            {
                await submitButton.ClickAsync();
            }
            else
            {
                await playwrightPage.Page.PressAsync(codeInputSelector, "Enter");
            }

            await playwrightPage.Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
            await Task.Delay(2000, cancellationToken);

            var currentUrl = playwrightPage.Page.Url;
            _logger.LogInformation("URL después de enviar OTP: {Url}", currentUrl);

            if (!currentUrl.Contains("checkpoint") && !currentUrl.Contains("two_factor"))
            {
                _logger.LogInformation("OTP aceptado correctamente");
                return true;
            }

            _logger.LogWarning("OTP rechazado o estado desconocido");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar código OTP");
            return false;
        }
    }
}
