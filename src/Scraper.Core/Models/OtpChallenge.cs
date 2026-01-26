namespace Scraper.Core.Models;

public class OtpChallenge
{
    public Guid Id { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public OtpChallengeStatus Status { get; set; }
    public string? Code { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }
}

public enum OtpChallengeStatus
{
    Pending,
    Submitted,
    Used,
    Expired
}
