namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Place;

public sealed class ListPlacesRequest
{
    [ITicketQueryName("page")]
    public int? Page { get; init; }

    [ITicketQueryName("per_page")]
    public int? PerPage { get; init; }

    [ITicketQueryName("tag[]")]
    public List<string?>? Tag { get; init; }

    [ITicketQueryName("show_category[]")]
    public List<string?>? ShowCategory { get; init; }

    [ITicketQueryName("amenity[]")]
    public List<string?>? Amenity { get; init; }

    [ITicketQueryName("province[]")]
    public List<int>? Province { get; init; }

    [ITicketQueryName("city[]")]
    public List<int>? City { get; init; }

    [ITicketQueryName("search")]
    public string? Search { get; init; }

    [ITicketQueryName("sort")]
    public string? Sort { get; init; }

}
