namespace Scraper.Core.Models;

public class FacebookGroupMetricDaily
{
    public int IdMetrica { get; set; }
    public int IdGrupo { get; set; }
    public DateTime Fecha { get; set; }
    public string ClaveMetrica { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime FechaObtencion { get; set; }
    public DateTime? FechaCreacion { get; set; }
}
