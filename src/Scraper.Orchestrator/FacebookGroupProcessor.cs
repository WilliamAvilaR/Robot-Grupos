using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Core.Services;

namespace Scraper.Orchestrator;

public class FacebookGroupProcessor
{
    private readonly ILogger<FacebookGroupProcessor> _logger;
    private readonly IBrowserClient _browserClient;
    private readonly IFacebookGroupRepository _groupRepository;
    private readonly IFacebookGroupMetricRepository _metricRepository;
    private readonly IPageExtractor<Dictionary<string, object>> _groupExtractor;
    private readonly AdaptiveTimingService _timingService;

    public FacebookGroupProcessor(
        ILogger<FacebookGroupProcessor> logger,
        IBrowserClient browserClient,
        IFacebookGroupRepository groupRepository,
        IFacebookGroupMetricRepository metricRepository,
        IPageExtractor<Dictionary<string, object>> groupExtractor,
        AdaptiveTimingService timingService)
    {
        _logger = logger;
        _browserClient = browserClient;
        _groupRepository = groupRepository;
        _metricRepository = metricRepository;
        _groupExtractor = groupExtractor;
        _timingService = timingService;
    }

    public async Task ProcessGroupsAsync(string accountId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando procesamiento de grupos de Facebook");

        var groups = await _groupRepository.GetActiveGroupsAsync(cancellationToken);
        var groupsList = groups.ToList();

        _logger.LogInformation("Se encontraron {Count} grupos para procesar", groupsList.Count);

        var page = await _browserClient.NewPageAsync(accountId, cancellationToken);

        try
        {
            for (int i = 0; i < groupsList.Count; i++)
            {
                var group = groupsList[i];
                
                try
                {
                    _logger.LogInformation("[{Current}/{Total}] Procesando grupo IdGrupo={IdGrupo}, URL={Url}", 
                        i + 1, groupsList.Count, group.IdGrupo, group.UrlOriginal);

                    // Navegar con medición de tiempo
                    var navigationResult = await _browserClient.GoToAsyncWithTiming(page, group.UrlOriginal, cancellationToken);
                    _timingService.RecordLoadTime(navigationResult.LoadTimeMs);
                    
                    // Delay adaptativo basado en tiempo de carga
                    var timingOptions = _timingService.GetOptions();
                    var adaptiveDelay = _timingService.CalculateAdaptiveDelay(
                        timingOptions.DelayAfterGroupNavigationMs, 
                        navigationResult.LoadTimeMs);
                    
                    _logger.LogDebug("Esperando {Delay}ms después de navegación (tiempo de carga: {LoadTime}ms)", 
                        adaptiveDelay, navigationResult.LoadTimeMs);
                    await Task.Delay(adaptiveDelay, cancellationToken);

                    var extractedData = await _groupExtractor.ExtractAsync(page, cancellationToken);

                    var nombre = extractedData.ContainsKey("nombre") ? extractedData["nombre"]?.ToString() : null;
                    var imagenUrl = extractedData.ContainsKey("imagenUrl") ? extractedData["imagenUrl"]?.ToString() : null;
                    DateTime? fechaCreacionFacebook = null;
                    decimal? postCount = null;
                    decimal? memberCount = null;

                    if (extractedData.ContainsKey("fechaCreacionFacebook") && 
                        !string.IsNullOrWhiteSpace(extractedData["fechaCreacionFacebook"]?.ToString()))
                    {
                        if (DateTime.TryParse(extractedData["fechaCreacionFacebook"]?.ToString(), out var fecha))
                        {
                            fechaCreacionFacebook = fecha;
                        }
                    }

                    if (extractedData.ContainsKey("postCount") && extractedData["postCount"] != null)
                    {
                        if (decimal.TryParse(extractedData["postCount"]?.ToString(), out var post))
                        {
                            postCount = post;
                        }
                    }

                    if (extractedData.ContainsKey("memberCount") && extractedData["memberCount"] != null)
                    {
                        if (decimal.TryParse(extractedData["memberCount"]?.ToString(), out var member))
                        {
                            memberCount = member;
                        }
                    }

                    var hasData = !string.IsNullOrWhiteSpace(nombre) || 
                                 !string.IsNullOrWhiteSpace(imagenUrl) || 
                                 fechaCreacionFacebook.HasValue;

                    if (hasData)
                    {
                        group.Nombre = nombre;
                        group.UrlImagen = imagenUrl;
                        group.FechaCreacionFacebook = fechaCreacionFacebook;
                        await _groupRepository.UpdateGroupAsync(group, cancellationToken);
                        
                        _logger.LogInformation("Grupo actualizado - IdGrupo={IdGrupo}, Nombre={Nombre}, Imagen={Imagen}, FechaCreacion={Fecha}", 
                            group.IdGrupo, nombre, imagenUrl, fechaCreacionFacebook?.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                    }
                    else
                    {
                        _logger.LogWarning("No se extrajeron datos del grupo IdGrupo={IdGrupo}", group.IdGrupo);
                    }

                    var today = DateTime.Now.Date;

                    if (postCount.HasValue)
                    {
                        var metricExists = await _metricRepository.MetricExistsAsync(group.IdGrupo, today, "post_count", cancellationToken);
                        if (!metricExists)
                        {
                            var metric = new FacebookGroupMetricDaily
                            {
                                IdGrupo = group.IdGrupo,
                                Fecha = today,
                                ClaveMetrica = "post_count",
                                Valor = postCount.Value,
                                FechaObtencion = DateTime.UtcNow
                            };
                            await _metricRepository.SaveMetricAsync(metric, cancellationToken);
                            _logger.LogInformation("Métrica post_count guardada: IdGrupo={IdGrupo}, Valor={Valor}", group.IdGrupo, postCount.Value);
                        }
                        else
                        {
                            _logger.LogInformation("Métrica post_count ya existe para hoy, omitiendo");
                        }
                    }

                    if (memberCount.HasValue)
                    {
                        var metricExists = await _metricRepository.MetricExistsAsync(group.IdGrupo, today, "member_count", cancellationToken);
                        if (!metricExists)
                        {
                            var metric = new FacebookGroupMetricDaily
                            {
                                IdGrupo = group.IdGrupo,
                                Fecha = today,
                                ClaveMetrica = "member_count",
                                Valor = memberCount.Value,
                                FechaObtencion = DateTime.UtcNow
                            };
                            await _metricRepository.SaveMetricAsync(metric, cancellationToken);
                            _logger.LogInformation("Métrica member_count guardada: IdGrupo={IdGrupo}, Valor={Valor}", group.IdGrupo, memberCount.Value);
                        }
                        else
                        {
                            _logger.LogInformation("Métrica member_count ya existe para hoy, omitiendo");
                        }
                    }

                    if (i < groupsList.Count - 1)
                    {
                        // Delay aleatorio entre grupos
                        var delaySeconds = _timingService.CalculateRandomDelayBetweenGroups();
                        _logger.LogInformation("Esperando {Seconds} segundos antes del siguiente grupo...", delaySeconds);
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al procesar grupo IdGrupo={IdGrupo}, URL={Url}", 
                        group.IdGrupo, group.UrlOriginal);
                }
            }

            _logger.LogInformation("Procesamiento de grupos completado");
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
