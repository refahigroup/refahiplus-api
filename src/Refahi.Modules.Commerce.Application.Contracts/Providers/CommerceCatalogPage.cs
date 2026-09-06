using Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;

namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceCatalogPage(
    IReadOnlyList<CommerceProductDto> Items, 
    int PageNumber, 
    int PageSize, 
    int TotalCount
)
{
    public IReadOnlyList<string> UnavailableProviders { get; init; } = [];
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
