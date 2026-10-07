namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Schedule;

public sealed class BoxOfficeRankingRequest
{
    [ITicketQueryName("year")]
    public string? Year { get; init; }

    [ITicketQueryName("limit")]
    public int? Limit { get; init; }

}
