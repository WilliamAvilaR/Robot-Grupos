using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface IFacebookGroupMetricRepository
{
    Task SaveMetricAsync(FacebookGroupMetricDaily metric, CancellationToken cancellationToken = default);
    Task<bool> MetricExistsAsync(int idGrupo, DateTime fecha, string claveMetrica, CancellationToken cancellationToken = default);
}
