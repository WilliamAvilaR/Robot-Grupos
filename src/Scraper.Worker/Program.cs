using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scraper.Auth;
using Scraper.Browser;
using Scraper.Core.Interfaces;
using Scraper.Core.Services;
using Scraper.Extraction;
using Scraper.Infrastructure;
using Scraper.Orchestrator;
using Scraper.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/scraper-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Services.AddSerilog();

var connectionString = builder.Configuration.GetConnectionString("DataColor") 
    ?? throw new InvalidOperationException("ConnectionString 'DataColor' no encontrada");

builder.Services.Configure<BrowserOptions>(builder.Configuration.GetSection("Browser"));
builder.Services.Configure<SessionStoreOptions>(builder.Configuration.GetSection("SessionStore"));

builder.Services.AddSingleton<IBrowserClient, PlaywrightBrowserClient>();
builder.Services.AddSingleton<ISessionStore, FileSessionStore>();
builder.Services.AddSingleton<IAuthDetector, FacebookAuthDetector>();
builder.Services.AddSingleton<ILoginFlow, FacebookLoginFlow>();
builder.Services.AddSingleton<IOtpChallengeRepository>(sp => 
    new SqlOtpChallengeRepository(
        sp.GetRequiredService<ILogger<SqlOtpChallengeRepository>>(),
        connectionString));
builder.Services.AddSingleton<IOtpFlow, FacebookOtpFlow>();
builder.Services.AddSingleton<IResultRepository>(sp =>
    new SqlResultRepository(
        sp.GetRequiredService<ILogger<SqlResultRepository>>(),
        connectionString));
builder.Services.AddSingleton<IFacebookGroupRepository>(sp =>
    new SqlFacebookGroupRepository(
        sp.GetRequiredService<ILogger<SqlFacebookGroupRepository>>(),
        connectionString));
builder.Services.AddSingleton<IFacebookGroupMetricRepository>(sp =>
    new SqlFacebookGroupMetricRepository(
        sp.GetRequiredService<ILogger<SqlFacebookGroupMetricRepository>>(),
        connectionString));

// Registrar servicio de timing adaptativo
builder.Services.AddSingleton<AdaptiveTimingService>();

builder.Services.AddSingleton<IPageExtractor<Dictionary<string, object>>, FacebookGroupExtractor>();
builder.Services.AddSingleton<FacebookGroupProcessor>();
builder.Services.AddSingleton<IExtractorRegistry>(sp =>
{
    var registry = new ExtractorRegistry(sp.GetRequiredService<ILogger<ExtractorRegistry>>());
    var extractor = sp.GetRequiredService<IPageExtractor<Dictionary<string, object>>>();
    registry.Register("facebook.com/groups", extractor);
    return registry;
});
builder.Services.AddSingleton<IScraperOrchestrator, ScraperOrchestrator>();
builder.Services.AddSingleton<IJobRunner, JobRunner>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

try
{
    Log.Information("Iniciando Scraper Worker...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplicación terminada inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}
