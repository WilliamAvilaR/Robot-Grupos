using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using CoreIPage = Scraper.Core.Interfaces.IPage;

namespace Scraper.Browser;

public class PlaywrightBrowserClient : IBrowserClient
{
    private readonly ILogger<PlaywrightBrowserClient> _logger;
    private readonly BrowserOptions _options;
    private readonly ScraperTimingOptions _timingOptions;
    private IPlaywright? _playwright;
    private readonly Dictionary<string, IBrowserContext> _contexts = new();

    public PlaywrightBrowserClient(
        ILogger<PlaywrightBrowserClient> logger,
        IOptions<BrowserOptions> options,
        IOptions<ScraperTimingOptions> timingOptions)
    {
        _logger = logger;
        _options = options.Value;
        _timingOptions = timingOptions.Value;
    }

    public async Task CreateContextAsync(string accountId, CancellationToken cancellationToken = default)
    {
        if (_contexts.ContainsKey(accountId))
        {
            _logger.LogInformation("Contexto ya existe para accountId: {AccountId}", accountId);
            return;
        }

        if (_playwright == null)
        {
            _logger.LogInformation("Inicializando Playwright...");
            _playwright = await Playwright.CreateAsync();
        }

        // Usar LaunchPersistentContextAsync para crear un perfil de usuario persistente
        // Esto funciona como un navegador normal: mantiene cookies, localStorage, etc. en disco
        var userDataDir = Path.Combine(_options.StorageStatePath, accountId, "userData");
        Directory.CreateDirectory(userDataDir);
        
        _logger.LogInformation("Creando contexto persistente para accountId: {AccountId} en: {UserDataDir}", accountId, userDataDir);
        
        var contextOptions = new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = _options.Headless,
            SlowMo = _options.SlowMo,
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            UserAgent = _options.UserAgent ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
        };

        // Crear contexto persistente - esto mantiene el perfil en disco como un navegador normal
        var context = await _playwright[_options.BrowserType].LaunchPersistentContextAsync(userDataDir, contextOptions);
        
        // Si existe un storageState.json previo, intentar migrar cookies al nuevo perfil persistente
        // Nota: La migración es opcional - el perfil persistente funcionará de todas formas
        var storageStatePath = Path.Combine(_options.StorageStatePath, accountId, "storageState.json");
        if (File.Exists(storageStatePath))
        {
            var fileInfo = new FileInfo(storageStatePath);
            if (fileInfo.Length > 0)
            {
                _logger.LogInformation("Intentando migrar storage state previo desde: {Path} (Tamaño: {Size} bytes) al perfil persistente", storageStatePath, fileInfo.Length);
                try
                {
                    var storageStateContent = await File.ReadAllTextAsync(storageStatePath, cancellationToken);
                    var storageStateJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(storageStateContent);
                    
                    if (storageStateJson.TryGetProperty("cookies", out var cookiesElement))
                    {
                        // Deserializar manualmente las cookies para manejar el formato de sameSite
                        var cookiesList = new List<Microsoft.Playwright.Cookie>();
                        foreach (var cookieElement in cookiesElement.EnumerateArray())
                        {
                            try
                            {
                                var cookie = new Microsoft.Playwright.Cookie
                                {
                                    Name = cookieElement.GetProperty("name").GetString() ?? "",
                                    Value = cookieElement.GetProperty("value").GetString() ?? "",
                                    Domain = cookieElement.GetProperty("domain").GetString() ?? "",
                                    Path = cookieElement.GetProperty("path").GetString() ?? "/"
                                };
                                
                                // Manejar sameSite de forma más flexible
                                if (cookieElement.TryGetProperty("sameSite", out var sameSiteElement))
                                {
                                    var sameSiteValue = sameSiteElement.GetString();
                                    if (!string.IsNullOrEmpty(sameSiteValue))
                                    {
                                        // Mapear valores comunes de sameSite
                                        cookie.SameSite = sameSiteValue.ToLowerInvariant() switch
                                        {
                                            "strict" => Microsoft.Playwright.SameSiteAttribute.Strict,
                                            "lax" => Microsoft.Playwright.SameSiteAttribute.Lax,
                                            "none" => Microsoft.Playwright.SameSiteAttribute.None,
                                            _ => Microsoft.Playwright.SameSiteAttribute.None
                                        };
                                    }
                                }
                                
                                // Manejar expires (debe ser float? - timestamp Unix)
                                if (cookieElement.TryGetProperty("expires", out var expiresElement))
                                {
                                    if (expiresElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                                    {
                                        var expiresValue = expiresElement.GetDouble();
                                        if (expiresValue > 0)
                                        {
                                            cookie.Expires = (float)expiresValue;
                                        }
                                    }
                                }
                                
                                // Manejar httpOnly y secure
                                if (cookieElement.TryGetProperty("httpOnly", out var httpOnlyElement))
                                {
                                    cookie.HttpOnly = httpOnlyElement.GetBoolean();
                                }
                                
                                if (cookieElement.TryGetProperty("secure", out var secureElement))
                                {
                                    cookie.Secure = secureElement.GetBoolean();
                                }
                                
                                cookiesList.Add(cookie);
                            }
                            catch (Exception cookieEx)
                            {
                                _logger.LogDebug(cookieEx, "Error al procesar una cookie individual, omitiendo");
                            }
                        }
                        
                        if (cookiesList.Count > 0)
                        {
                            await context.AddCookiesAsync(cookiesList.ToArray());
                            _logger.LogInformation("Cookies migradas exitosamente al perfil persistente ({Count} cookies)", cookiesList.Count);
                        }
                        else
                        {
                            _logger.LogInformation("No se encontraron cookies válidas para migrar");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // La migración es opcional - el perfil persistente funcionará de todas formas
                    _logger.LogInformation("No se pudo migrar el storage state previo (esto es normal si el formato cambió). El perfil persistente funcionará de todas formas. Error: {Message}", ex.Message);
                }
            }
        }
        
        _contexts[accountId] = context;
        _logger.LogInformation("Contexto persistente creado para accountId: {AccountId}. El perfil se guardará automáticamente en: {UserDataDir}", accountId, userDataDir);
    }


    public async Task<CoreIPage> NewPageAsync(string accountId, CancellationToken cancellationToken = default)
    {
        if (!_contexts.TryGetValue(accountId, out var context))
        {
            await CreateContextAsync(accountId, cancellationToken);
            context = _contexts[accountId];
        }

        var page = await context.NewPageAsync();
        return new PlaywrightPageWrapper(page);
    }

    public async Task GoToAsync(CoreIPage page, string url, CancellationToken cancellationToken = default)
    {
        var result = await GoToAsyncWithTiming(page, url, cancellationToken);
        // El método con timing ya maneja los errores, aquí solo llamamos al método con timing
    }

    public async Task<NavigationResult> GoToAsyncWithTiming(CoreIPage page, string url, CancellationToken cancellationToken = default)
    {
        if (page is not PlaywrightPageWrapper playwrightPage)
        {
            throw new ArgumentException("Page debe ser una instancia de PlaywrightPageWrapper", nameof(page));
        }

        _logger.LogInformation("Navegando a: {Url}", url);
        
        var startTime = DateTime.UtcNow;
        try
        {
            await playwrightPage.Page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = _timingOptions.NavigationTimeoutMs
            });
            
            var loadTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            
            _logger.LogDebug("Navegación completada en {LoadTime:F0}ms", loadTime);
            
            return new NavigationResult
            {
                Success = true,
                LoadTimeMs = (long)loadTime
            };
        }
        catch (Microsoft.Playwright.PlaywrightException ex) when (ex.Message.Contains("Timeout"))
        {
            var loadTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogWarning(
                "Timeout al navegar a {Url} después de {LoadTime:F0}ms, pero la página puede haber cargado parcialmente", 
                url, loadTime);
            
            return new NavigationResult
            {
                Success = false,
                LoadTimeMs = (long)loadTime
            };
        }
    }

    public async Task WaitForSelectorAsync(CoreIPage page, string selector, int timeoutMs = 30000, CancellationToken cancellationToken = default)
    {
        if (page is not PlaywrightPageWrapper playwrightPage)
        {
            throw new ArgumentException("Page debe ser una instancia de PlaywrightPageWrapper", nameof(page));
        }

        await playwrightPage.Page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
        {
            Timeout = timeoutMs
        });
    }

    public async Task<T?> EvaluateAsync<T>(CoreIPage page, string script, CancellationToken cancellationToken = default)
    {
        if (page is not PlaywrightPageWrapper playwrightPage)
        {
            throw new ArgumentException("Page debe ser una instancia de PlaywrightPageWrapper", nameof(page));
        }

        var result = await playwrightPage.Page.EvaluateAsync<T>(script);
        return result;
    }

    public async Task<byte[]?> ScreenshotAsync(CoreIPage page, CancellationToken cancellationToken = default)
    {
        if (page is not PlaywrightPageWrapper playwrightPage)
        {
            throw new ArgumentException("Page debe ser una instancia de PlaywrightPageWrapper", nameof(page));
        }

        return await playwrightPage.Page.ScreenshotAsync();
    }

    public async Task<string?> GetContentAsync(CoreIPage page, CancellationToken cancellationToken = default)
    {
        if (page is not PlaywrightPageWrapper playwrightPage)
        {
            throw new ArgumentException("Page debe ser una instancia de PlaywrightPageWrapper", nameof(page));
        }

        return await playwrightPage.Page.ContentAsync();
    }

    public async Task SaveStorageStateAsync(string accountId, CancellationToken cancellationToken = default)
    {
        if (!_contexts.TryGetValue(accountId, out var context))
        {
            _logger.LogWarning("No se encontró contexto para accountId: {AccountId}", accountId);
            return;
        }

        var storageStatePath = Path.Combine(_options.StorageStatePath, accountId);
        Directory.CreateDirectory(storageStatePath);
        var fullPath = Path.Combine(storageStatePath, "storageState.json");

        _logger.LogInformation("Guardando storage state en: {Path}", fullPath);
        
        try
        {
            // Obtener el storage state del contexto
            var storageState = await context.StorageStateAsync();
            
            if (string.IsNullOrWhiteSpace(storageState))
            {
                _logger.LogWarning("El storage state está vacío. Puede que la sesión no se haya guardado correctamente.");
                return;
            }
            
            // Verificar que el storage state contiene cookies importantes
            var hasImportantCookies = storageState.Contains("\"cookies\"") && 
                                     (storageState.Contains("c_user") || storageState.Contains("xs") || storageState.Contains("datr"));
            
            if (!hasImportantCookies)
            {
                _logger.LogWarning("El storage state no contiene cookies de sesión importantes. Puede que la sesión no persista correctamente.");
            }
            
            // Guardar el archivo
            await File.WriteAllTextAsync(fullPath, storageState, cancellationToken);
            
            // Verificar que el archivo se guardó correctamente
            if (File.Exists(fullPath))
            {
                var fileInfo = new FileInfo(fullPath);
                _logger.LogInformation("Storage state guardado exitosamente en: {Path} (Tamaño: {Size} bytes)", fullPath, fileInfo.Length);
                
                if (fileInfo.Length == 0)
                {
                    _logger.LogWarning("El archivo de storage state está vacío. La sesión puede no persistir correctamente.");
                }
                else if (fileInfo.Length < 500)
                {
                    _logger.LogWarning("El archivo de storage state es muy pequeño ({Size} bytes). Puede que falten cookies importantes.", fileInfo.Length);
                }
            }
            else
            {
                _logger.LogError("El archivo de storage state no se creó en: {Path}", fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar storage state en: {Path}", fullPath);
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var context in _contexts.Values)
        {
            context?.DisposeAsync().AsTask().Wait();
        }
        _playwright?.Dispose();
    }
}

public class BrowserOptions
{
    public string BrowserType { get; set; } = "chromium";
    public bool Headless { get; set; } = false;
    public int? SlowMo { get; set; }
    public string? UserAgent { get; set; }
    public string StorageStatePath { get; set; } = @"C:\ScraperData\state";
}
