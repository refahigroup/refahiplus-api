#nullable enable

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ListShowPlacesRequest
{
    public string Show { get; init; }

    [ITicketQueryName("days")]
    public int? Days { get; init; }

    [ITicketQueryName("place[]")]
    public List<string?>? Place { get; init; }

    [ITicketQueryName("province[]")]
    public List<int>? Province { get; init; }

    [ITicketQueryName("city[]")]
    public List<int>? City { get; init; }

    [ITicketQueryName("search")]
    public string? Search { get; init; }

}
