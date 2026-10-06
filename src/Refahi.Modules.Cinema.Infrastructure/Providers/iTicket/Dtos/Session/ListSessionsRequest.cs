namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Session;

public sealed class ListSessionsRequest
{
    public string Show { get; init; }

    public string Place { get; init; }

    [ITicketQueryName("date")]
    public string? Date { get; init; }

}
