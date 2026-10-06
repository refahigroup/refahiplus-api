#nullable enable

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ListSessionsRequest
{
    public string Show { get; init; }

    public string Place { get; init; }

    [ITicketQueryName("date")]
    public string? Date { get; init; }

}
