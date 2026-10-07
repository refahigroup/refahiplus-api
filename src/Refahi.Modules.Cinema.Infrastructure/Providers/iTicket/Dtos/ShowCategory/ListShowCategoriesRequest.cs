namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ShowCategory;

public sealed class ListShowCategoriesRequest
{
    [ITicketQueryName("page")]
    public int? Page { get; init; }

    [ITicketQueryName("per_page")]
    public int? PerPage { get; init; }

    [ITicketQueryName("search")]
    public string? Search { get; init; }

    [ITicketQueryName("parent")]
    public string? Parent { get; init; }

    [ITicketQueryName("all")]
    public bool? All { get; init; }

    [ITicketQueryName("include[]")]
    public List<string?>? Include { get; init; }

    [ITicketQueryName("exclude[]")]
    public List<string?>? Exclude { get; init; }

}
