using MediatR;
using Refahi.Modules.Commerce.Application.Contracts.Providers;

namespace Refahi.Modules.Commerce.Application.Features.Catalog.GetProducts;

public sealed record GetCommerceProductsQuery(
    string? Search, 
    string? ProviderKey, 
    int PageNumber = 1, 
    int PageSize = 24
) : IRequest<CommerceCatalogPage>;

