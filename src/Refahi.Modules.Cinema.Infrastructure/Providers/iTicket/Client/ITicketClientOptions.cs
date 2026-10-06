namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Client;

public sealed class ITicketClientOptions
{
    public string BaseUrl { get; init; } = "https://console.iticket.ir/api/v1/";
    public string ApiAccessToken { get; init; } = string.Empty;
}
