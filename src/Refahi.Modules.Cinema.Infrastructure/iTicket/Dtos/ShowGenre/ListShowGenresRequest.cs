#nullable enable

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ListShowGenresRequest
{
    [ITicketQueryName("page")]
    public int? Page { get; init; }

    [ITicketQueryName("per_page")]
    public int? PerPage { get; init; }

    [ITicketQueryName("search")]
    public string? Search { get; init; }

}
