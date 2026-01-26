using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;

namespace Scraper.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IJobRunner _jobRunner;

    public Worker(ILogger<Worker> logger, IJobRunner jobRunner)
    {
        _logger = logger;
        _jobRunner = jobRunner;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker iniciado - Procesando grupos de Facebook");

        try
        {
            _logger.LogInformation("=== INICIANDO AUTENTICACIÓN ===");
            
            var jobRunner = _jobRunner as JobRunner;
            if (jobRunner == null)
            {
                throw new InvalidOperationException("JobRunner no es del tipo esperado");
            }

            await jobRunner.Orchestrator.EnsureAuthenticatedAsync("default", stoppingToken);
            
            _logger.LogInformation("=== AUTENTICACIÓN COMPLETADA ===");
            _logger.LogInformation("=== INICIANDO PROCESAMIENTO DE GRUPOS ===");
            
            await jobRunner.Orchestrator.ProcessFacebookGroupsAsync("default", stoppingToken);
            
            _logger.LogInformation("=== PROCESAMIENTO COMPLETADO ===");
            _logger.LogInformation("Presiona Ctrl+C para detener el worker.");
            
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el procesamiento");
            throw;
        }
    }
}
