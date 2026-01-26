using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;

namespace Scraper.Worker;

public class JobRunner : IJobRunner
{
    private readonly ILogger<JobRunner> _logger;
    private readonly IScraperOrchestrator _orchestrator;

    public JobRunner(
        ILogger<JobRunner> logger,
        IScraperOrchestrator orchestrator)
    {
        _logger = logger;
        _orchestrator = orchestrator;
    }

    internal IScraperOrchestrator Orchestrator => _orchestrator;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("JobRunner iniciado");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("JobRunner detenido");
        return Task.CompletedTask;
    }

    public async Task RunJobAsync(string accountId, IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ejecutando job para accountId: {AccountId}", accountId);
        await _orchestrator.RunScrapeJobAsync(accountId, urls, cancellationToken);
    }
}
