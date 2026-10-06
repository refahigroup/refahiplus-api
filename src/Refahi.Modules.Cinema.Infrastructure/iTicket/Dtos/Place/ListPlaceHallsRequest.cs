#nullable enable

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ListPlaceHallsRequest
{
    public string Place { get; init; }

    [ITicketQueryName("page")]
    public int? Page { get; init; }

    [ITicketQueryName("per_page")]
    public int? PerPage { get; init; }

}
