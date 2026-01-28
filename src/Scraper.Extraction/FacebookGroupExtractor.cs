using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Core.Services;
using System.Text.Json;

namespace Scraper.Extraction;

public class FacebookGroupExtractor : IPageExtractor<Dictionary<string, object>>
{
    private readonly ILogger<FacebookGroupExtractor> _logger;
    private readonly IBrowserClient _browserClient;
    private readonly IAuthDetector _authDetector;
    private readonly AdaptiveTimingService _timingService;

    public FacebookGroupExtractor(
        ILogger<FacebookGroupExtractor> logger,
        IBrowserClient browserClient,
        IAuthDetector authDetector,
        AdaptiveTimingService timingService)
    {
        _logger = logger;
        _browserClient = browserClient;
        _authDetector = authDetector;
        _timingService = timingService;
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

            await Task.Delay(3000, cancellationToken);

            var nombre = await ExtractGroupNameAsync(playwrightPage, cancellationToken);
            var imagenUrl = await ExtractGroupImageAsync(playwrightPage, cancellationToken);

            result["nombre"] = nombre ?? string.Empty;
            result["imagenUrl"] = imagenUrl ?? string.Empty;

            _logger.LogInformation("Extraído grupo - Nombre: {Nombre}, Imagen: {Imagen}", nombre, imagenUrl);

            var fechaCreacion = await ExtractCreationDateAsync(playwrightPage, cancellationToken);
            if (fechaCreacion.HasValue)
            {
                result["fechaCreacionFacebook"] = fechaCreacion.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");
                _logger.LogInformation("Fecha de creación extraída: {Fecha}", fechaCreacion.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            }

            var postCount = await ExtractPostCountAsync(playwrightPage, cancellationToken);
            if (postCount.HasValue)
            {
                result["postCount"] = postCount.Value;
                _logger.LogInformation("Post count extraído: {Count}", postCount.Value);
            }

            var memberCount = await ExtractMemberCountAsync(playwrightPage, cancellationToken);
            if (memberCount.HasValue)
            {
                result["memberCount"] = memberCount.Value;
                _logger.LogInformation("Member count extraído: {Count}", memberCount.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer datos del grupo");
            result["error"] = ex.Message;
        }

        return result;
    }

    private async Task<string?> ExtractGroupNameAsync(Browser.PlaywrightPageWrapper page, CancellationToken cancellationToken)
    {
        try
        {
            var nombreSelector = "h1 a[href*='/groups/']";
            
            var nombreElement = await page.Page.QuerySelectorAsync(nombreSelector);
            if (nombreElement != null)
            {
                var nombre = await nombreElement.TextContentAsync();
                if (!string.IsNullOrWhiteSpace(nombre))
                {
                    return nombre.Trim();
                }
            }

            var nombreAltSelector = "h1 span a[href*='/groups/']";
            nombreElement = await page.Page.QuerySelectorAsync(nombreAltSelector);
            if (nombreElement != null)
            {
                var nombre = await nombreElement.TextContentAsync();
                if (!string.IsNullOrWhiteSpace(nombre))
                {
                    return nombre.Trim();
                }
            }

            _logger.LogWarning("No se encontró el nombre del grupo");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer nombre del grupo");
            return null;
        }
    }

    private async Task<string?> ExtractGroupImageAsync(Browser.PlaywrightPageWrapper page, CancellationToken cancellationToken)
    {
        try
        {
            var imagenSelectors = new[]
            {
                "a[aria-label='Foto de portada'] img",
                "div[role='img'] img[src*='fbcdn.net']",
                "img[data-imgperflogname='profileCoverPhoto']"
            };

            foreach (var selector in imagenSelectors)
            {
                try
                {
                    var imagenElement = await page.Page.QuerySelectorAsync(selector);
                    if (imagenElement != null)
                    {
                        var src = await imagenElement.GetAttributeAsync("src");
                        if (!string.IsNullOrWhiteSpace(src))
                        {
                            var cleanUrl = CleanImageUrl(src);
                            if (!string.IsNullOrWhiteSpace(cleanUrl))
                            {
                                return cleanUrl;
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            _logger.LogWarning("No se encontró la imagen del grupo");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer imagen del grupo");
            return null;
        }
    }

    private string? CleanImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var cleanUrl = url.Trim();
        
        if (cleanUrl.Contains("&amp;"))
        {
            cleanUrl = cleanUrl.Replace("&amp;", "&");
        }

        if (cleanUrl.Contains("_nc_cat=") && cleanUrl.Contains("fbcdn.net"))
        {
            return cleanUrl;
        }

        return null;
    }

    private async Task<DateTime?> ExtractCreationDateAsync(Browser.PlaywrightPageWrapper page, CancellationToken cancellationToken)
    {
        try
        {
            var currentUrl = page.Page.Url;
            var aboutUrl = currentUrl.EndsWith("/about") ? currentUrl : currentUrl.TrimEnd('/') + "/about";

              _logger.LogInformation("Navegando a página About para extraer fecha de creación: {Url}", aboutUrl);
              try
              {
                  await page.Page.GotoAsync(aboutUrl, new Microsoft.Playwright.PageGotoOptions
                  {
                      WaitUntil = Microsoft.Playwright.WaitUntilState.Load,
                      Timeout = 30000
                  });
              }
              catch (Microsoft.Playwright.PlaywrightException ex) when (ex.Message.Contains("Timeout"))
              {
                  _logger.LogWarning("Timeout al navegar a {Url}, pero la página puede haber cargado parcialmente. Continuando...", aboutUrl);
                  // La página puede haber cargado parcialmente, continuamos de todas formas
              }

            await Task.Delay(2000, cancellationToken);

            var fechaText = await ExtractCreationDateTextAsync(page, cancellationToken);
            if (string.IsNullOrWhiteSpace(fechaText))
            {
                _logger.LogWarning("No se encontró el texto de fecha de creación");
                return null;
            }

            var fecha = ParseSpanishDate(fechaText);
            return fecha;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer fecha de creación");
            return null;
        }
    }

    private async Task<string?> ExtractCreationDateTextAsync(Browser.PlaywrightPageWrapper page, CancellationToken cancellationToken)
    {
        try
        {
            var selectors = new[]
            {
                "span:has-text('Grupo creado el')",
                "div:has-text('Grupo creado el')",
                "span[dir='auto']:has-text('Grupo creado')"
            };

            foreach (var selector in selectors)
            {
                try
                {
                    var element = await page.Page.QuerySelectorAsync(selector);
                    if (element != null)
                    {
                        var text = await element.TextContentAsync();
                        if (!string.IsNullOrWhiteSpace(text) && text.Contains("Grupo creado"))
                        {
                            return text.Trim();
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            var pageContent = await page.Page.ContentAsync();
            if (!string.IsNullOrWhiteSpace(pageContent))
            {
                var textContent = System.Text.RegularExpressions.Regex.Replace(pageContent, "<[^>]+>", " ");
                var lines = textContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("Grupo creado el"))
                    {
                        return line.Trim();
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer texto de fecha");
            return null;
        }
    }

    private DateTime? ParseSpanishDate(string dateText)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dateText))
                return null;

            var text = dateText.Trim();
            
            if (!text.Contains("Grupo creado el"))
                return null;

            var datePart = text.Substring(text.IndexOf("Grupo creado el") + "Grupo creado el".Length).Trim();
            
            if (datePart.Contains("."))
            {
                datePart = datePart.Substring(0, datePart.IndexOf("."));
            }
            
            if (datePart.Contains("El nombre"))
            {
                datePart = datePart.Substring(0, datePart.IndexOf("El nombre"));
            }
            
            datePart = datePart.Trim();
            
            var months = new Dictionary<string, int>
            {
                { "enero", 1 }, { "febrero", 2 }, { "marzo", 3 }, { "abril", 4 },
                { "mayo", 5 }, { "junio", 6 }, { "julio", 7 }, { "agosto", 8 },
                { "septiembre", 9 }, { "octubre", 10 }, { "noviembre", 11 }, { "diciembre", 12 }
            };

            var words = datePart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            int day = 0;
            int month = 0;
            int year = 0;

            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i].Trim().ToLower().TrimEnd('.', ',');
                
                if (int.TryParse(word, out var num))
                {
                    if (num >= 1 && num <= 31 && day == 0)
                    {
                        day = num;
                    }
                    else if (num >= 1900 && num <= 2100 && year == 0)
                    {
                        year = num;
                    }
                }
                else if (word == "de" && i > 0 && i < words.Length - 1)
                {
                    var prevWord = words[i - 1].Trim().ToLower();
                    var nextWord = words[i + 1].Trim().ToLower();
                    
                    if (int.TryParse(prevWord, out var dayNum) && months.ContainsKey(nextWord))
                    {
                        day = dayNum;
                        month = months[nextWord];
                    }
                }
                else if (months.ContainsKey(word) && month == 0)
                {
                    month = months[word];
                }
            }

            if (day > 0 && month > 0 && year > 0)
            {
                var date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
                _logger.LogInformation("Fecha parseada exitosamente: {Date} desde texto: {Text}", date.ToString("yyyy-MM-ddTHH:mm:ssZ"), datePart);
                return date;
            }

            _logger.LogWarning("No se pudo parsear la fecha. Day={Day}, Month={Month}, Year={Year} desde texto: {DateText}", day, month, year, datePart);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al parsear fecha en español: {DateText}", dateText);
            return null;
        }
    }

    private async Task<decimal?> ExtractPostCountAsync(Browser.PlaywrightPageWrapper page, CancellationToken cancellationToken)
    {
        try
        {
            var currentUrl = page.Page.Url;
            
            if (!currentUrl.Contains("/about"))
            {
                var aboutUrl = currentUrl.TrimEnd('/') + "/about";
                _logger.LogInformation("Navegando a página About para extraer post count: {Url}", aboutUrl);
                await _browserClient.GoToAsync(page, aboutUrl, cancellationToken);
                var timingOptions = _timingService.GetOptions();
              var delay = _timingService.CalculateRandomDelay(timingOptions.DelayAfterAboutPageMs);
              await Task.Delay(delay, cancellationToken);
            }

            var selectors = new[]
            {
                "span:has-text('publicaciones nuevas hoy')",
                "div:has-text('publicaciones nuevas hoy')",
                "span[dir='auto']:has-text('publicaciones nuevas hoy')"
            };

            foreach (var selector in selectors)
            {
                try
                {
                    var element = await page.Page.QuerySelectorAsync(selector);
                    if (element != null)
                    {
                        var text = await element.TextContentAsync();
                        if (!string.IsNullOrWhiteSpace(text) && text.Contains("publicaciones nuevas hoy"))
                        {
                            // Detectar explícitamente el caso de "No hay publicaciones nuevas hoy"
                            var textLower = text.ToLower();
                            if (textLower.Contains("no hay publicaciones nuevas hoy") || 
                                (textLower.Contains("no hay") && textLower.Contains("publicaciones nuevas hoy")))
                            {
                                _logger.LogInformation("Se detectó 'No hay publicaciones nuevas hoy', retornando 0");
                                return 0;
                            }
                            
                            var count = ExtractNumberFromText(text);
                            if (count.HasValue)
                            {
                                return count.Value;
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            _logger.LogWarning("No se encontró el post count");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer post count");
            return null;
        }
    }

    private async Task<decimal?> ExtractMemberCountAsync(Browser.PlaywrightPageWrapper page, CancellationToken cancellationToken)
    {
        try
        {
            var currentUrl = page.Page.Url;
            
            if (!currentUrl.Contains("/members"))
            {
                // Navegar directamente desde la página del grupo a /members (la página del grupo ya está autenticada)
                var baseUrl = currentUrl.Split(new[] { "/about" }, StringSplitOptions.None)[0].TrimEnd('/');
                var membersUrl = baseUrl + "/members";
                _logger.LogInformation("Navegando a página Members para extraer member count: {Url}", membersUrl);
                _logger.LogInformation("Nota: /members es la única página que requiere login estricto. Si muestra login, se omitirá esta métrica.");
                await _browserClient.GoToAsync(page, membersUrl, cancellationToken);
                
                // Verificar la URL después de navegar (puede haber sido redirigida)
                var timingOptions = _timingService.GetOptions();
                var delay = _timingService.CalculateRandomDelay(timingOptions.DelayAfterMembersPageMs);
                await Task.Delay(delay, cancellationToken);
                var finalUrl = page.Page.Url;
                _logger.LogInformation("URL después de navegar a /members: {Url}", finalUrl);
                
                // Verificar si fue redirigida a login o checkpoint
                if (finalUrl.Contains("login") || finalUrl.Contains("checkpoint") || finalUrl.Contains("two_factor"))
                {
                    _logger.LogWarning("La página fue redirigida a login/checkpoint. Sesión puede haber expirado. URL: {Url}", finalUrl);
                    return null;
                }
                
                // Verificar si la URL es diferente a la esperada
                if (!finalUrl.Contains("/members"))
                {
                    _logger.LogWarning("La URL no contiene '/members' después de navegar. URL actual: {Url}", finalUrl);
                }
                
                // Verificar el contenido de la página para detectar página de login (aunque la URL no cambie)
                var bodyCheckElement = await page.Page.QuerySelectorAsync("body");
                if (bodyCheckElement != null)
                {
                    var bodyText = await bodyCheckElement.TextContentAsync();
                    if (!string.IsNullOrWhiteSpace(bodyText))
                    {
                        var loginIndicators = new[] { "Únete a Facebook", "Iniciar sesión", "Correo electrónico o teléfono", "Contraseña", "¿Has olvidado los datos de la cuenta?" };
                        var isLoginPage = loginIndicators.Any(indicator => bodyText.Contains(indicator, StringComparison.OrdinalIgnoreCase));
                        
                        if (isLoginPage)
                        {
                            _logger.LogInformation("La página /members requiere autenticación adicional. Omitiendo extracción de member count (esto es normal, /members es la única página que requiere login estricto).");
                            return null;
                        }
                    }
                }
            }

            // Esperar hasta que aparezca al menos un h2 (la página está cargada)
            _logger.LogInformation("Esperando a que la página cargue (buscando elemento h2)...");
            var maxWaitTime = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;
            var h2Found = false;

            while ((DateTime.UtcNow - startTime) < maxWaitTime && !h2Found)
            {
                var h2Elements = await page.Page.QuerySelectorAllAsync("h2");
                var currentUrlCheck = page.Page.Url;
                
                // Verificar si la URL cambió durante la espera (redirección)
                if (currentUrlCheck.Contains("login") || currentUrlCheck.Contains("checkpoint"))
                {
                    _logger.LogWarning("Detectada redirección a login/checkpoint durante la espera. URL: {Url}", currentUrlCheck);
                    return null;
                }
                
                // Verificar el contenido de la página para detectar página de login
                var bodyWaitElement = await page.Page.QuerySelectorAsync("body");
                if (bodyWaitElement != null)
                {
                    var bodyText = await bodyWaitElement.TextContentAsync();
                    if (!string.IsNullOrWhiteSpace(bodyText))
                    {
                        var loginIndicators = new[] { "Únete a Facebook", "Iniciar sesión", "Correo electrónico o teléfono", "Contraseña" };
                        var isLoginPage = loginIndicators.Any(indicator => bodyText.Contains(indicator, StringComparison.OrdinalIgnoreCase));
                        
                        if (isLoginPage)
                        {
                            _logger.LogWarning("Detectada página de login en el contenido durante la espera. Sesión puede haber expirado.");
                            return null;
                        }
                    }
                }
                
                if (h2Elements.Count > 0)
                {
                    h2Found = true;
                    _logger.LogInformation("Página cargada: encontrados {Count} elementos h2", h2Elements.Count);
                    
                    // Verificar el contenido de los h2 para debugging
                    foreach (var h2 in h2Elements.Take(3))
                    {
                        var h2Text = await h2.TextContentAsync();
                        _logger.LogDebug("Contenido de h2: {Text}", h2Text);
                    }
                    
                    var timingOptionsH2 = _timingService.GetOptions();
                    var delayH2 = _timingService.CalculateRandomDelay(timingOptionsH2.DelayWaitForH2ElementsMs);
                    await Task.Delay(delayH2, cancellationToken); // Esperar un poco más para que termine de renderizar
                    break;
                }
                
                _logger.LogDebug("Esperando carga de página... (h2 encontrados: {Count}, URL: {Url})", h2Elements.Count, currentUrlCheck);
                var timingOptionsLoop = _timingService.GetOptions();
                var delayLoop = _timingService.CalculateRandomDelay(timingOptionsLoop.DelayWaitForPageLoadMs);
                await Task.Delay(delayLoop, cancellationToken); // Esperar antes de volver a intentar
            }

            if (!h2Found)
            {
                // Intentar obtener más información sobre qué hay en la página
                var pageTitle = await page.Page.TitleAsync();
                var bodyText = await page.Page.QuerySelectorAsync("body");
                var bodyTextContent = bodyText != null ? await bodyText.TextContentAsync() : null;
                var bodyPreview = bodyTextContent != null ? bodyTextContent.Substring(0, Math.Min(200, bodyTextContent.Length)) : "N/A";
                
                _logger.LogWarning("Timeout esperando a que la página cargue (no se encontraron elementos h2). Título: {Title}, URL: {Url}, Body preview: {BodyPreview}", 
                    pageTitle, page.Page.Url, bodyPreview);
                return null;
            }

            // Intentar con JavaScript primero (más confiable)
            var scriptResult = await _browserClient.EvaluateAsync<string>(page, @"
                (() => {
                    const h2Elements = Array.from(document.querySelectorAll('h2'));
                    for (const h2 of h2Elements) {
                        const text = h2.textContent || h2.innerText || '';
                        if (text.includes('Miembros')) {
                            const strongElements = h2.querySelectorAll('strong');
                            for (const strong of strongElements) {
                                const strongText = strong.textContent || strong.innerText || '';
                                const match = strongText.match(/(\d{1,3}(?:\.\d{3})+)/);
                                if (match) {
                                    return match[1];
                                }
                            }
                            const match = text.match(/Miembros[^0-9]*(\d{1,3}(?:\.\d{3})+)/);
                            if (match) {
                                return match[1];
                            }
                        }
                    }
                    const allText = document.body.textContent || document.body.innerText || '';
                    const match = allText.match(/Miembros[^0-9]*(\d{1,3}(?:\.\d{3})+)/);
                    return match ? match[1] : null;
                })();
            ", cancellationToken);
            
            if (!string.IsNullOrWhiteSpace(scriptResult))
            {
                var count = ExtractNumberFromText(scriptResult);
                if (count.HasValue && count.Value > 0)
                {
                    _logger.LogInformation("Member count encontrado con JavaScript: {Count}", count.Value);
                    return count.Value;
                }
            }

            // Buscar en todos los h2
            var allH2Elements = await page.Page.QuerySelectorAllAsync("h2");
            _logger.LogDebug("Buscando en {Count} elementos h2", allH2Elements.Count);
            
            foreach (var h2 in allH2Elements)
            {
                try
                {
                    var h2Text = await h2.TextContentAsync();
                    if (!string.IsNullOrWhiteSpace(h2Text) && h2Text.Contains("Miembros"))
                    {
                        _logger.LogDebug("Encontrado h2 con 'Miembros': {Text}", h2Text);
                        
                        // Buscar directamente el número en el texto completo del h2
                        var count = ExtractNumberFromText(h2Text);
                        if (count.HasValue && count.Value > 0)
                        {
                            _logger.LogInformation("Member count encontrado en h2 (texto completo): {Count}", count.Value);
                            return count.Value;
                        }
                        
                        // Buscar en todos los strong dentro del h2
                        var strongElements = await h2.QuerySelectorAllAsync("strong");
                        _logger.LogDebug("Encontrados {Count} elementos strong dentro del h2", strongElements.Count);
                        
                        foreach (var strong in strongElements)
                        {
                            var strongText = await strong.TextContentAsync();
                            if (!string.IsNullOrWhiteSpace(strongText))
                            {
                                _logger.LogDebug("Texto en strong dentro de h2: {Text}", strongText);
                                count = ExtractNumberFromText(strongText);
                                if (count.HasValue && count.Value > 0)
                                {
                                    _logger.LogInformation("Member count encontrado en strong dentro de h2: {Count}", count.Value);
                                    return count.Value;
                                }
                            }
                        }
                        
                        // Buscar recursivamente en todos los span dentro del h2
                        var allSpans = await h2.QuerySelectorAllAsync("span");
                        _logger.LogDebug("Encontrados {Count} elementos span dentro del h2", allSpans.Count);
                        
                        foreach (var span in allSpans)
                        {
                            var spanText = await span.TextContentAsync();
                            if (!string.IsNullOrWhiteSpace(spanText))
                            {
                                // Buscar strong dentro de este span
                                var strongInSpan = await span.QuerySelectorAsync("strong");
                                if (strongInSpan != null)
                                {
                                    var strongText = await strongInSpan.TextContentAsync();
                                    if (!string.IsNullOrWhiteSpace(strongText))
                                    {
                                        _logger.LogDebug("Texto en strong dentro de span: {Text}", strongText);
                                        count = ExtractNumberFromText(strongText);
                                        if (count.HasValue && count.Value > 0)
                                        {
                                            _logger.LogInformation("Member count encontrado en strong dentro de span: {Count}", count.Value);
                                            return count.Value;
                                        }
                                    }
                                }
                                
                                // También buscar directamente en el texto del span si contiene números
                                if (spanText.Contains("Miembros"))
                                {
                                    count = ExtractNumberFromText(spanText);
                                    if (count.HasValue && count.Value > 0)
                                    {
                                        _logger.LogInformation("Member count encontrado en span con 'Miembros': {Count}", count.Value);
                                        return count.Value;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error procesando h2");
                    continue;
                }
            }

            // Último recurso: buscar en el HTML completo
            var pageContent = await _browserClient.GetContentAsync(page, cancellationToken);
            if (!string.IsNullOrWhiteSpace(pageContent))
            {
                _logger.LogDebug("Buscando 'Miembros' en el contenido HTML completo...");
                
                var patterns = new[]
                {
                    @"Miembros[^>]*>([^<]*<[^>]*>)*([0-9]{1,3}(?:\.[0-9]{3})+)",  
                    @"Miembros.*?<strong>.*?([0-9]{1,3}(?:\.[0-9]{3})+)",  
                    @"Miembros[^0-9]*([0-9]{1,3}(?:\.[0-9]{3})+)",  
                };

                foreach (var pattern in patterns)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(pageContent, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                    if (match.Success)
                    {
                        var numberStr = match.Groups[match.Groups.Count - 1].Value;
                        _logger.LogDebug("Patrón '{Pattern}' encontró: {NumberStr}", pattern, numberStr);
                        var count = ExtractNumberFromText(numberStr);
                        if (count.HasValue && count.Value > 0)
                        {
                            _logger.LogInformation("Member count encontrado en HTML con patrón '{Pattern}': {Count}", pattern, count.Value);
                            return count.Value;
                        }
                    }
                }
            }

            var bodyElement = await page.Page.QuerySelectorAsync("body");
            if (bodyElement != null)
            {
                var allText = await bodyElement.TextContentAsync();
                if (!string.IsNullOrWhiteSpace(allText))
                {
                    _logger.LogDebug("Buscando en body text completo (primeros 500 caracteres): {Text}", allText.Substring(0, Math.Min(500, allText.Length)));
                    
                    var memberMatch = System.Text.RegularExpressions.Regex.Match(allText, @"Miembros[^0-9]*([0-9]{1,3}(?:\.[0-9]{3})+)");
                    if (memberMatch.Success)
                    {
                        var count = ExtractNumberFromText(memberMatch.Groups[1].Value);
                        if (count.HasValue)
                        {
                            _logger.LogInformation("Member count encontrado con regex en body: {Count}", count.Value);
                            return count.Value;
                        }
                    }
                }
            }

            _logger.LogWarning("No se encontró el member count después de todos los intentos");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer member count");
            return null;
        }
    }

    private decimal? ExtractNumberFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var cleanText = text.Trim();
        
        _logger.LogDebug("Extrayendo número de texto: {Text}", cleanText);
        
        var numberPatterns = new[]
        {
            @"(\d{1,3}(?:\.\d{3})+)",  
            @"(\d{1,3}(?:,\d{3})+)",  
            @"(\d+)",                  
        };

        foreach (var pattern in numberPatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(cleanText, pattern);
            if (match.Success)
            {
                var numberStr = match.Groups[1].Value;
                
                // Detectar si el punto/comma es separador de miles o decimal
                if (numberStr.Contains("."))
                {
                    var parts = numberStr.Split('.');
                    // Si tiene más de 1 parte y todas las partes después de la primera tienen exactamente 3 dígitos, es separador de miles
                    if (parts.Length > 1 && parts.Skip(1).All(p => p.Length == 3))
                    {
                        // Es separador de miles: eliminar todos los puntos
                        numberStr = numberStr.Replace(".", "");
                        _logger.LogDebug("Detectado separador de miles (punto), número sin separadores: {NumberStr}", numberStr);
                    }
                }
                else if (numberStr.Contains(","))
                {
                    var parts = numberStr.Split(',');
                    // Similar lógica para comas
                    if (parts.Length > 1 && parts.Skip(1).All(p => p.Length == 3))
                    {
                        numberStr = numberStr.Replace(",", "");
                        _logger.LogDebug("Detectado separador de miles (coma), número sin separadores: {NumberStr}", numberStr);
                    }
                }
                
                if (decimal.TryParse(numberStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var number))
                {
                    _logger.LogDebug("Número parseado: {Number} desde texto: {OriginalText}", number, match.Groups[1].Value);
                    return number;
                }
            }
        }
        
        _logger.LogWarning("No se pudo extraer número de texto: {Text}", cleanText);
        return null;
    }
}
