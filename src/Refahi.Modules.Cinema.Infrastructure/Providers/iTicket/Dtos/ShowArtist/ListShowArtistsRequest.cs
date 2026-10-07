namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ShowArtist;

public sealed class ListShowArtistsRequest
{
    [ITicketQueryName("page")]
    public int? Page { get; init; }

    [ITicketQueryName("per_page")]
    public int? PerPage { get; init; }

    [ITicketQueryName("search")]
    public string? Search { get; init; }

    [ITicketQueryName("role[]")]
    public List<string?>? Role { get; init; }

}
