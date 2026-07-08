namespace Refahi.Modules.Charge.Infrastructure.Providers.Eniac;
public sealed class EniacOptions
{
    public const string Section = "Charge:Providers:Eniac";
    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int ChannelId { get; set; } = 102;
    public string? ResellerName { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public int TokenRefreshSkewSeconds { get; set; } = 120;
    public int UnresolvedTimeoutHours { get; set; } = 24;
    public string UnresolvedAction { get; set; } = "ManualReview";
}
