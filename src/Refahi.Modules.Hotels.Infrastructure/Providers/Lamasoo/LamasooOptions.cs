namespace Refahi.Modules.Hotels.Infrastructure.Providers.Lamasoo;

public sealed class LamasooOptions
{
    public string BaseUrl { get; set; } = "https://api.lamasoo.com/";
    public string ApiKey { get; set; } = "";
    public bool SearchEnabled { get; set; }
    public bool PurchaseEnabled { get; set; }
    public bool ContractVerified { get; set; }
    public bool IncludeAgencyProvider { get; set; }
    public int TimeoutSeconds { get; set; } = 8;
    // Explicit, reviewed city mapping: Lamasoo city id -> existing SnappTrip city id.
}
