namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ShowGenre;

public sealed class ListShowGenresRequest
{
    [ITicketQueryName("page")]
    public int? Page { get; init; }

    [ITicketQueryName("per_page")]
    public int? PerPage { get; init; }

    [ITicketQueryName("search")]
    public string? Search { get; init; }

}
