namespace Refahi.Modules.Entertainments.Application.Contracts.Providers.Requests;

public sealed record EntertainmentCatalogQuery(
    string? Search = null, 
    string? ProviderKey = null,
    string? SupplierKey = null,
    int PageNumber = 1, 
    int PageSize = 24, string? LocationCode = null, string? CategoryCode = null
);
