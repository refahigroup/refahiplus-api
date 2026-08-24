namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceCatalogPage(
    IReadOnlyList<CommerceProductDto> Items, 
    int PageNumber, 
    int PageSize, 
    int TotalCount
);
