namespace Refahi.Modules.Commerce.Application.Contracts.Providers.Requests;

public sealed record CommerceCatalogQuery(
    string? Search = null, 
    string? ProviderKey = null,
    string? SellerKey = null,
    int PageNumber = 1, 
    int PageSize = 24
);
