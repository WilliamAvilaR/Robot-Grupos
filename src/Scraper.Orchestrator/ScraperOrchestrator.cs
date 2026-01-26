using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Core.Services;
using Scraper.Browser;
using CoreIPage = Scraper.Core.Interfaces.IPage;

namespace Scraper.Orchestrator;

public class ScraperOrchestrator : IScraperOrchestrator
{
    private readonly ILogger<ScraperOrchestrator> _logger;
    private readonly IBrowserClient _browserClient;
    private readonly IAuthDetector _authDetector;
    private readonly ILoginFlow _loginFlow;
    private readonly IOtpFlow _otpFlow;
    private readonly IExtractorRegistry _extractorRegistry;
    private readonly IResultRepository _resultRepository;
    private readonly ISessionStore _sessionStore;
    private readonly IOtpChallengeRepository _otpRepository;
    private readonly IConfiguration _configuration;
    private readonly FacebookGroupProcessor _groupProcessor;
    private readonly AdaptiveTimingService _timingService;

    public ScraperOrchestrator(
        ILogger<ScraperOrchestrator> logger,
        IBrowserClient browserClient,
        IAuthDetector authDetector,
        ILoginFlow loginFlow,
        IOtpFlow otpFlow,
        IExtractorRegistry extractorRegistry,
        IResultRepository resultRepository,
        ISessionStore sessionStore,
        IOtpChallengeRepository otpRepository,
        IConfiguration configuration,
        FacebookGroupProcessor groupProcessor,
        AdaptiveTimingService timingService)
    {
        _logger = logger;
        _browserClient = browserClient;
        _authDetector = authDetector;
        _loginFlow = loginFlow;
        _otpFlow = otpFlow;
        _extractorRegistry = extractorRegistry;
        _resultRepository = resultRepository;
        _sessionStore = sessionStore;
        _otpRepository = otpRepository;
        _configuration = configuration;
        _groupProcessor = groupProcessor;
        _timingService = timingService;
    }

    public async Task RunScrapeJobAsync(string accountId, IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando job de scraping para accountId: {AccountId} con {Count} URLs", accountId, urls.Count());

        await EnsureAuthenticatedAsync(accountId, cancellationToken);

        var page = await _browserClient.NewPageAsync(accountId, cancellationToken);

        try
        {
            foreach (var url in urls)
            {
                try
                {
                    _logger.LogInformation("Procesando URL: {Url}", url);
                    await _browserClient.GoToAsync(page, url, cancellationToken);

                    var extractor = _extractorRegistry.GetExtractor<System.Collections.Generic.Dictionary<string, object>>(url);
                    if (extractor != null)
                    {
                        var extractedData = await extractor.ExtractAsync(page, cancellationToken);
                        
                        var result = new ScrapeResult
                        {
                            AccountId = accountId,
                            Url = url,
                            CapturedAt = DateTime.UtcNow,
                            PayloadJson = System.Text.Json.JsonSerializer.Serialize(extractedData)
                        };

                        await _resultRepository.SaveAsync(result, cancellationToken);
                        _logger.LogInformation("Datos extraídos y guardados para URL: {Url}", url);
                    }
                    else
                    {
                        _logger.LogWarning("No se encontró extractor para URL: {Url}", url);
                    }

                    var timingOptions = _timingService.GetOptions();
                    var delay = _timingService.CalculateRandomDelay(timingOptions.DelayBetweenUrlsMs);
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al procesar URL: {Url}", url);
                }
            }
        }
        finally
        {
            await page.CloseAsync();
            await _browserClient.SaveStorageStateAsync(accountId, cancellationToken);
        }
    }

    public async Task EnsureAuthenticatedAsync(string accountId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verificando autenticación para accountId: {AccountId}", accountId);

        await _browserClient.CreateContextAsync(accountId, cancellationToken);
        var page = await _browserClient.NewPageAsync(accountId, cancellationToken);

        try
        {
            await _browserClient.GoToAsync(page, "https://www.facebook.com", cancellationToken);
            
            var authState = await _authDetector.GetAuthStateAsync(page, cancellationToken);

            switch (authState)
            {
                case AuthState.Authenticated:
                    _logger.LogInformation("Usuario ya autenticado");
                    // Navegar al feed para asegurar que todas las cookies estén establecidas
                    _logger.LogInformation("Navegando al feed para verificar cookies de sesión...");
                    await _browserClient.GoToAsync(page, "https://www.facebook.com", cancellationToken);
                    var timingOpts1 = _timingService.GetOptions();
                    var delay1 = _timingService.CalculateRandomDelay(timingOpts1.DelayAfterAuthCheckMs);
                    await Task.Delay(delay1, cancellationToken);
                    // Guardar storage state para mantenerlo actualizado
                    await _browserClient.SaveStorageStateAsync(accountId, cancellationToken);
                    var timingOpts2 = _timingService.GetOptions();
                    var delay2 = _timingService.CalculateRandomDelay(timingOpts2.DelayAfterSaveStateMs);
                    await Task.Delay(delay2, cancellationToken);
                    break;

                case AuthState.NeedsLogin:
                    _logger.LogInformation("Se requiere login");
                    await PerformLoginIfNeededAsync(accountId, page, cancellationToken);
                    break;

                case AuthState.NeedsOtp:
                    _logger.LogInformation("Se requiere OTP");
                    var challengeId = await _otpFlow.RequestOtpChallengeAsync(accountId, null, cancellationToken);
                    await HandleOtpFlowAsync(challengeId, cancellationToken);
                    break;

                case AuthState.Blocked:
                    throw new InvalidOperationException("Cuenta bloqueada o suspendida");

                default:
                    _logger.LogWarning("Estado de autenticación desconocido");
                    break;
            }
        }
        finally
        {
            // Guardar storage state ANTES de cerrar la página para asegurar que se capture la sesión completa
            await _browserClient.SaveStorageStateAsync(accountId, cancellationToken);
            await Task.Delay(1000, cancellationToken); // Esperar a que se guarde completamente
            
            await page.CloseAsync();
            
            // Guardar storage state una vez más después de cerrar (por si acaso)
            await _browserClient.SaveStorageStateAsync(accountId, cancellationToken);
            await Task.Delay(_timingService.GetDelayShort(), cancellationToken);
        }
    }

    public async Task HandleOtpFlowAsync(Guid challengeId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Manejando flujo OTP para challengeId: {ChallengeId}", challengeId);
        _logger.LogInformation("Esperando que el usuario inserte el código OTP en la base de datos...");
        _logger.LogInformation("Para insertar el código, ejecuta: UPDATE OtpChallenge SET Status = 'Submitted', Code = 'TU_CODIGO' WHERE Id = '{ChallengeId}'", challengeId);

        var code = await _otpFlow.WaitOtpFromDbAsync(challengeId, 300, cancellationToken);
        
        if (string.IsNullOrEmpty(code))
        {
            throw new InvalidOperationException("No se recibió código OTP o expiró el tiempo de espera");
        }

        _logger.LogInformation("Código OTP recibido, enviando a la página...");
        
        var challenge = await _otpRepository.GetByIdAsync(challengeId, cancellationToken);
        if (challenge == null)
        {
            throw new InvalidOperationException($"Challenge no encontrado: {challengeId}");
        }

        var page = await _browserClient.NewPageAsync(challenge.AccountId, cancellationToken);
        
        try
        {
            var success = await _otpFlow.SubmitOtpToPageAsync(page, code, cancellationToken);
            if (!success)
            {
                throw new InvalidOperationException("El código OTP fue rechazado");
            }

            // Navegar al feed de Facebook para que establezca todas las cookies de sesión
            _logger.LogInformation("Navegando al feed de Facebook para establecer cookies de sesión completas...");
            await _browserClient.GoToAsync(page, "https://www.facebook.com", cancellationToken);
            await Task.Delay(3000, cancellationToken);
            
            // Hacer scroll y algunas interacciones para que Facebook establezca todas las cookies
            if (page is Browser.PlaywrightPageWrapper playwrightPage)
            {
                _logger.LogInformation("Realizando interacciones para establecer cookies de sesión...");
                try
                {
                    // Esperar a que el feed cargue
                    await Task.Delay(_timingService.GetDelayAfterLogin(), cancellationToken);
                    
                    // Hacer scroll para activar la carga de contenido
                    await playwrightPage.Page.EvaluateAsync("window.scrollTo(0, 500)");
                    await Task.Delay(_timingService.GetDelayAfterInteraction(), cancellationToken);
                    await playwrightPage.Page.EvaluateAsync("window.scrollTo(0, 0)");
                    await Task.Delay(_timingService.GetDelayAfterInteraction(), cancellationToken);
                    
                        // Intentar hacer clic en el área del feed para activar más cookies
                        try
                        {
                            var feedSelector = "[role='main']";
                            var feedElement = await playwrightPage.Page.QuerySelectorAsync(feedSelector);
                            if (feedElement != null)
                            {
                                await feedElement.ClickAsync();
                                await Task.Delay(_timingService.GetDelayAfterInteraction(), cancellationToken);
                                _logger.LogInformation("Clic realizado en el feed");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "No se pudo hacer clic en el feed, continuando...");
                        }
                    
                    // Esperar adicional para que Facebook procese las interacciones
                    await Task.Delay(_timingService.GetDelayAfterLogin(), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error al hacer interacciones, continuando...");
                }
            }
            
            // Verificar que realmente estamos autenticados antes de guardar
            var finalAuthState = await _authDetector.GetAuthStateAsync(page, cancellationToken);
            if (finalAuthState == AuthState.Authenticated)
            {
                _logger.LogInformation("Sesión verificada, esperando a que Facebook establezca todas las cookies...");
                await Task.Delay(3000, cancellationToken); // Esperar adicional para cookies de sesión
                
                _logger.LogInformation("Guardando storage state con sesión completa...");
                await _browserClient.SaveStorageStateAsync(challenge.AccountId, cancellationToken);
                await Task.Delay(_timingService.GetDelayAfterSaveState(), cancellationToken); // Esperar a que se guarde completamente
                _logger.LogInformation("OTP procesado exitosamente y storage state guardado");
            }
            else
            {
                _logger.LogWarning("Después del OTP, el estado de autenticación es: {AuthState}. Guardando storage state de todas formas...", finalAuthState);
                await _browserClient.SaveStorageStateAsync(challenge.AccountId, cancellationToken);
                await Task.Delay(_timingService.GetDelayAfterSaveState(), cancellationToken);
            }
        }
        finally
        {
            await page.CloseAsync();
            // Guardar storage state una vez más antes de cerrar para asegurar que esté actualizado
            await _browserClient.SaveStorageStateAsync(challenge.AccountId, cancellationToken);
            await Task.Delay(_timingService.GetDelayShort(), cancellationToken);
        }
    }

    private async Task PerformLoginIfNeededAsync(string accountId, CoreIPage page, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Intentando realizar login...");
        
        var email = _configuration["Facebook:Email"];
        var password = _configuration["Facebook:Password"];

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("Las credenciales de Facebook no están configuradas en appsettings.json. Configura 'Facebook:Email' y 'Facebook:Password'");
        }

        var credentials = new FacebookCredentials
        {
            Email = email,
            Password = password
        };

        var loginSuccess = await _loginFlow.PerformLoginAsync(page, credentials, cancellationToken);
        
        if (!loginSuccess)
        {
            _logger.LogWarning("El login no fue exitoso. Verificando si se requiere OTP...");
            await Task.Delay(_timingService.GetDelayAfterAuthCheck(), cancellationToken);
            
            var newAuthState = await _authDetector.GetAuthStateAsync(page, cancellationToken);
            if (newAuthState == AuthState.NeedsOtp)
            {
                _logger.LogInformation("Se requiere OTP después del login");
                var challengeId = await _otpFlow.RequestOtpChallengeAsync(accountId, null, cancellationToken);
                await HandleOtpFlowAsync(challengeId, cancellationToken);
            }
            else if (newAuthState == AuthState.Authenticated)
            {
                _logger.LogInformation("Login exitoso después de verificación");
                // Navegar al feed de Facebook para que establezca todas las cookies de sesión
                _logger.LogInformation("Navegando al feed de Facebook para establecer cookies de sesión completas...");
                await _browserClient.GoToAsync(page, "https://www.facebook.com", cancellationToken);
                await Task.Delay(_timingService.GetDelayAfterLogin(), cancellationToken);
                
                // Hacer scroll y algunas interacciones para que Facebook establezca todas las cookies
                if (page is Browser.PlaywrightPageWrapper playwrightPage)
                {
                    _logger.LogInformation("Realizando interacciones para establecer cookies de sesión...");
                    try
                    {
                        // Esperar a que el feed cargue
                        await Task.Delay(_timingService.GetDelayAfterLogin(), cancellationToken);
                        
                        await playwrightPage.Page.EvaluateAsync("window.scrollTo(0, 500)");
                        await Task.Delay(_timingService.GetDelayAfterInteraction(), cancellationToken);
                        await playwrightPage.Page.EvaluateAsync("window.scrollTo(0, 0)");
                        await Task.Delay(_timingService.GetDelayAfterInteraction(), cancellationToken);
                        
                        // Intentar hacer clic en el área del feed
                        try
                        {
                            var feedSelector = "[role='main']";
                            var feedElement = await playwrightPage.Page.QuerySelectorAsync(feedSelector);
                            if (feedElement != null)
                            {
                                await feedElement.ClickAsync();
                                await Task.Delay(_timingService.GetDelayAfterInteraction(), cancellationToken);
                                _logger.LogInformation("Clic realizado en el feed");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "No se pudo hacer clic en el feed, continuando...");
                        }
                        
                        await Task.Delay(_timingService.GetDelayAfterLogin(), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error al hacer interacciones, continuando...");
                    }
                }
                
                // Esperar adicional para que Facebook establezca todas las cookies
                await Task.Delay(_timingService.GetDelayAfterAuthCheck(), cancellationToken);
                await _browserClient.SaveStorageStateAsync(accountId, cancellationToken);
                await Task.Delay(_timingService.GetDelayAfterSaveState(), cancellationToken);
            }
            else
            {
                throw new InvalidOperationException($"Login fallido. Estado de autenticación: {newAuthState}");
            }
        }
        else
        {
            _logger.LogInformation("Login exitoso");
            // Navegar al feed de Facebook para que establezca todas las cookies de sesión
            _logger.LogInformation("Navegando al feed de Facebook para establecer cookies de sesión completas...");
            await _browserClient.GoToAsync(page, "https://www.facebook.com", cancellationToken);
            await Task.Delay(_timingService.GetDelayAfterLogin(), cancellationToken);
            
            // Hacer scroll y algunas interacciones para que Facebook establezca todas las cookies
            if (page is Browser.PlaywrightPageWrapper playwrightPage)
            {
                _logger.LogInformation("Realizando interacciones para establecer cookies de sesión...");
                try
                {
                    // Esperar a que el feed cargue
                    await Task.Delay(_timingService.GetDelayAfterLogin(), cancellationToken);
                    
                    await playwrightPage.Page.EvaluateAsync("window.scrollTo(0, 500)");
                    await Task.Delay(_timingService.GetDelayAfterInteraction(), cancellationToken);
                    await playwrightPage.Page.EvaluateAsync("window.scrollTo(0, 0)");
                    await Task.Delay(_timingService.GetDelayAfterInteraction(), cancellationToken);
                    
                    // Intentar hacer clic en el área del feed
                    try
                    {
                        var feedSelector = "[role='main']";
                        var feedElement = await playwrightPage.Page.QuerySelectorAsync(feedSelector);
                        if (feedElement != null)
                        {
                            await feedElement.ClickAsync();
                            await Task.Delay(_timingService.GetDelayAfterInteraction(), cancellationToken);
                            _logger.LogInformation("Clic realizado en el feed");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "No se pudo hacer clic en el feed, continuando...");
                    }
                    
                    await Task.Delay(_timingService.GetDelayAfterLogin(), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error al hacer interacciones, continuando...");
                }
            }
            
            // Esperar adicional para que Facebook establezca todas las cookies
            await Task.Delay(_timingService.GetDelayAfterAuthCheck(), cancellationToken);
            await _browserClient.SaveStorageStateAsync(accountId, cancellationToken);
            await Task.Delay(_timingService.GetDelayAfterSaveState(), cancellationToken);
        }
    }

    public async Task ProcessFacebookGroupsAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await _groupProcessor.ProcessGroupsAsync(accountId, cancellationToken);
    }
}
