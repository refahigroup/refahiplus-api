namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket;

public sealed class iTiketOptions
{
    public string BaseUrl { get; set; } = "https://console.iticket.ir/api/v1/";
    public string AccessToken { get; set; } = string.Empty;
}
