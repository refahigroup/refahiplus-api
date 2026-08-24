namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceCatalogQuery(
    string? Search = null, 
    string? ProviderKey = null, 
    int PageNumber = 1, 
    int PageSize = 24
);
