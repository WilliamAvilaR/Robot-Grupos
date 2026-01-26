using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scraper.Core.Models;

namespace Scraper.Core.Services;

public class AdaptiveTimingService
{
    private readonly ILogger<AdaptiveTimingService> _logger;
    private readonly ScraperTimingOptions _options;
    private readonly Random _random;
    
    // Historial de tiempos de carga (para calcular promedio)
    private readonly Queue<long> _loadTimeHistory = new();
    private const int HistorySize = 10; // Mantener últimos 10 tiempos
    
    public AdaptiveTimingService(
        ILogger<AdaptiveTimingService> logger,
        IOptions<ScraperTimingOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _random = new Random();
    }
    
    /// <summary>
    /// Calcula delay adaptativo basado en el tiempo real de carga
    /// </summary>
    public int CalculateAdaptiveDelay(int baseDelayMs, long actualLoadTimeMs)
    {
        // Determinar multiplicador según velocidad de carga
        double multiplier = actualLoadTimeMs < _options.FastLoadThresholdMs 
            ? _options.FastLoadMultiplier
            : actualLoadTimeMs > _options.SlowLoadThresholdMs
                ? _options.SlowLoadMultiplier
                : _options.NormalLoadMultiplier;
        
        // Aplicar multiplicador
        var adjustedDelay = (int)(baseDelayMs * multiplier);
        
        // Agregar variación aleatoria
        var variation = adjustedDelay * _options.RandomVariationPercent;
        var randomVariation = _random.NextDouble() * variation * 2 - variation; // ±variation
        adjustedDelay = (int)(adjustedDelay + randomVariation);
        
        // Aplicar límites mínimos y máximos
        adjustedDelay = Math.Max(_options.MinDelayMs, adjustedDelay);
        adjustedDelay = Math.Min(_options.MaxDelayMs, adjustedDelay);
        
        _logger.LogDebug(
            "Delay adaptativo: Base={Base}ms, LoadTime={LoadTime}ms, Multiplier={Multiplier}, Final={Final}ms",
            baseDelayMs, actualLoadTimeMs, multiplier, adjustedDelay);
        
        return adjustedDelay;
    }
    
    /// <summary>
    /// Calcula delay aleatorio entre grupos
    /// </summary>
    public int CalculateRandomDelayBetweenGroups()
    {
        var delay = _random.Next(
            _options.DelayBetweenGroupsMinSeconds,
            _options.DelayBetweenGroupsMaxSeconds + 1);
        
        _logger.LogDebug("Delay aleatorio entre grupos: {Delay}s", delay);
        return delay;
    }
    
    /// <summary>
    /// Calcula delay con variación aleatoria (sin adaptación)
    /// </summary>
    public int CalculateRandomDelay(int baseDelayMs)
    {
        var variation = baseDelayMs * _options.RandomVariationPercent;
        var randomVariation = _random.NextDouble() * variation * 2 - variation;
        var delay = (int)(baseDelayMs + randomVariation);
        
        delay = Math.Max(_options.MinDelayMs, delay);
        delay = Math.Min(_options.MaxDelayMs, delay);
        
        return delay;
    }
    
    /// <summary>
    /// Registra tiempo de carga para análisis futuro
    /// </summary>
    public void RecordLoadTime(long loadTimeMs)
    {
        _loadTimeHistory.Enqueue(loadTimeMs);
        if (_loadTimeHistory.Count > HistorySize)
        {
            _loadTimeHistory.Dequeue();
        }
        
        var average = _loadTimeHistory.Count > 0 
            ? _loadTimeHistory.Average() 
            : loadTimeMs;
        
        _logger.LogDebug("Tiempo de carga registrado: {LoadTime}ms, Promedio: {Average:F0}ms", 
            loadTimeMs, average);
    }
    
    /// <summary>
    /// Obtiene el tiempo promedio de carga reciente
    /// </summary>
    public double GetAverageLoadTime()
    {
        return _loadTimeHistory.Count > 0 
            ? _loadTimeHistory.Average() 
            : (_options.FastLoadThresholdMs + _options.SlowLoadThresholdMs) / 2.0;
    }
    
    /// <summary>
    /// Obtiene las opciones de timing actuales
    /// </summary>
    public ScraperTimingOptions GetOptions() => _options;
    
    /// <summary>
    /// Calcula delay aleatorio para después de guardar estado
    /// </summary>
    public int GetDelayAfterSaveState() => CalculateRandomDelay(_options.DelayAfterSaveStateMs);
    
    /// <summary>
    /// Calcula delay aleatorio para después de login
    /// </summary>
    public int GetDelayAfterLogin() => CalculateRandomDelay(_options.DelayAfterLoginMs);
    
    /// <summary>
    /// Calcula delay aleatorio para después de OTP
    /// </summary>
    public int GetDelayAfterOtp() => CalculateRandomDelay(_options.DelayAfterOtpMs);
    
    /// <summary>
    /// Calcula delay aleatorio para después de verificación de auth
    /// </summary>
    public int GetDelayAfterAuthCheck() => CalculateRandomDelay(_options.DelayAfterAuthCheckMs);
    
    /// <summary>
    /// Calcula delay aleatorio para interacciones
    /// </summary>
    public int GetDelayAfterInteraction() => CalculateRandomDelay(_options.DelayAfterInteractionBaseMs);
    
    /// <summary>
    /// Calcula delay aleatorio corto
    /// </summary>
    public int GetDelayShort() => CalculateRandomDelay(_options.DelayShortMs);
    
    /// <summary>
    /// Calcula delay aleatorio entre URLs
    /// </summary>
    public int GetDelayBetweenUrls() => CalculateRandomDelay(_options.DelayBetweenUrlsMs);
}
