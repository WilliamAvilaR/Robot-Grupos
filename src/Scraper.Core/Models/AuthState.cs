namespace Scraper.Core.Models;

public enum AuthState
{
    Authenticated,
    NeedsLogin,
    NeedsOtp,
    Blocked,
    Unknown
}
