#nullable enable

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ListResellerSchedulesRequest
{
    [ITicketQueryName("page")]
    public int? Page { get; init; }

    [ITicketQueryName("per_page")]
    public int? PerPage { get; init; }

    [ITicketQueryName("place[]")]
    public List<string?>? Place { get; init; }

    [ITicketQueryName("province[]")]
    public List<int>? Province { get; init; }

    [ITicketQueryName("city[]")]
    public List<int>? City { get; init; }

    [ITicketQueryName("category[]")]
    public List<string?>? Category { get; init; }

    [ITicketQueryName("starts_from")]
    public string? StartsFrom { get; init; }

    [ITicketQueryName("starts_to")]
    public string? StartsTo { get; init; }

}
