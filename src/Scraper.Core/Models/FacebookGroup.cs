namespace Scraper.Core.Models;

public class FacebookGroup
{
    public int IdGrupo { get; set; }
    public int? IdUsuario { get; set; }
    public string? FacebookGroupId { get; set; }
    public string UrlOriginal { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? UrlImagen { get; set; }
    public bool? Activo { get; set; }
    public DateTime? FechaUltimaSincronizacion { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public DateTime? FechaCreacionFacebook { get; set; }
}
