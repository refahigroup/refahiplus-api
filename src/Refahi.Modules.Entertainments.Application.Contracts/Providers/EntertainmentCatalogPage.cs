using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;

namespace Refahi.Modules.Entertainments.Application.Contracts.Providers;

public sealed record EntertainmentCatalogPage(
    IReadOnlyList<EntertainmentProgramDto> Items, 
    int PageNumber, 
    int PageSize, 
    int TotalCount
)
{
    public IReadOnlyList<string> UnavailableProviders { get; init; } = [];
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
