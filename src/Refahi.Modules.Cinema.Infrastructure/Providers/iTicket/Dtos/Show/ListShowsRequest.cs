namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Show;

public sealed class ListShowsRequest
{
    [ITicketQueryName("page")]
    public int? Page { get; init; }

    [ITicketQueryName("per_page")]
    public int? PerPage { get; init; }

    [ITicketQueryName("category[]")]
    public List<string?>? Category { get; init; }

    [ITicketQueryName("tag[]")]
    public List<string?>? Tag { get; init; }

    [ITicketQueryName("genre[]")]
    public List<string?>? Genre { get; init; }

    [ITicketQueryName("screening[]")]
    public List<string?>? Screening { get; init; }

    [ITicketQueryName("artist[]")]
    public List<string?>? Artist { get; init; }

    [ITicketQueryName("search")]
    public string? Search { get; init; }

    [ITicketQueryName("sort")]
    public string? Sort { get; init; }

    [ITicketQueryName("province[]")]
    public List<int>? Province { get; init; }

    [ITicketQueryName("city[]")]
    public List<int>? City { get; init; }

    [ITicketQueryName("only_with_active_sessions")]
    public bool? OnlyWithActiveSessions { get; init; }

}
