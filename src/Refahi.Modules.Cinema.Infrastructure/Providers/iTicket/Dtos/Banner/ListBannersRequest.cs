namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Banner;

public sealed class ListBannersRequest
{
    [ITicketQueryName("placement")]
    public string? Placement { get; init; }

    [ITicketQueryName("page")]
    public int? Page { get; init; }

    [ITicketQueryName("per_page")]
    public int? PerPage { get; init; }

}
