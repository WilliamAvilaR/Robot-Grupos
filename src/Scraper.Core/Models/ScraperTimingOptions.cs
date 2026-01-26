namespace Scraper.Core.Models;

public class ScraperTimingOptions
{
    // Delays base (en milisegundos) - se ajustarán según velocidad de carga
    public int DelayAfterNavigationBaseMs { get; set; } = 2000;
    public int DelayAfterPageLoadBaseMs { get; set; } = 1500;
    public int DelayAfterInteractionBaseMs { get; set; } = 1500;
    public int DelayAfterSaveStateMs { get; set; } = 1000;
    public int DelayShortMs { get; set; } = 500;
    
    // Delays específicos de autenticación
    public int DelayAfterLoginMs { get; set; } = 3000;
    public int DelayAfterOtpMs { get; set; } = 3000;
    public int DelayAfterAuthCheckMs { get; set; } = 2000;
    
    // Delays específicos de extracción
    public int DelayAfterGroupNavigationMs { get; set; } = 2000;
    public int DelayAfterAboutPageMs { get; set; } = 1500;
    public int DelayAfterMembersPageMs { get; set; } = 2000;
    public int DelayWaitForPageLoadMs { get; set; } = 1000;
    public int DelayWaitForH2ElementsMs { get; set; } = 2000;
    
    // Multiplicadores adaptativos según velocidad de carga
    public double FastLoadMultiplier { get; set; } = 0.8;  // Si carga < 2s
    public double NormalLoadMultiplier { get; set; } = 1.0; // Si carga 2-5s
    public double SlowLoadMultiplier { get; set; } = 1.5;   // Si carga > 5s
    
    // Variación aleatoria (porcentaje)
    public double RandomVariationPercent { get; set; } = 0.25; // ±25%
    
    // Delays mínimos y máximos (límites de seguridad)
    public int MinDelayMs { get; set; } = 1000;  // Nunca menos de 1s
    public int MaxDelayMs { get; set; } = 10000;  // Nunca más de 10s
    
    // Delays entre grupos (con variación aleatoria)
    public int DelayBetweenGroupsMinSeconds { get; set; } = 15;
    public int DelayBetweenGroupsMaxSeconds { get; set; } = 30;
    
    // Delays entre URLs
    public int DelayBetweenUrlsMs { get; set; } = 2000;
    
    // Timeouts
    public int NavigationTimeoutMs { get; set; } = 30000;
    public int SelectorTimeoutMs { get; set; } = 10000;
    public int PageLoadTimeoutMs { get; set; } = 30000;
    
    // Umbrales para determinar velocidad de carga
    public int FastLoadThresholdMs { get; set; } = 2000;  // < 2s = rápido
    public int SlowLoadThresholdMs { get; set; } = 5000;  // > 5s = lento
    
    // Timeouts de espera
    public int OtpWaitTimeoutSeconds { get; set; } = 300;
    public int MaxWaitForH2ElementsSeconds { get; set; } = 30;
}
