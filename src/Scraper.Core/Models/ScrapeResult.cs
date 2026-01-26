namespace Scraper.Core.Models;

public class ScrapeResult
{
    public Guid Id { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public string? PayloadJson { get; set; }
    public string? ContentHash { get; set; }
    public string? HtmlSnapshotPath { get; set; }
}
