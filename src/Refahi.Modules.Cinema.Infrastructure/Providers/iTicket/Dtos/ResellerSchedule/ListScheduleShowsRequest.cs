namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ResellerSchedule;

public sealed class ListScheduleShowsRequest
{
    [ITicketQueryName("days")]
    public int? Days { get; init; }

    [ITicketQueryName("place[]")]
    public List<string?>? Place { get; init; }

    [ITicketQueryName("province[]")]
    public List<int>? Province { get; init; }

    [ITicketQueryName("city[]")]
    public List<int>? City { get; init; }

    [ITicketQueryName("category[]")]
    public List<string?>? Category { get; init; }

    [ITicketQueryName("search")]
    public string? Search { get; init; }

}
