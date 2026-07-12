using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Products;

public sealed record GetProductsQuery(
    int ModuleId,
    string? SearchQuery = null,
    string Sort = "newest",
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<ProductsPagedResponse>;

public sealed record ProductsPagedResponse(
    IEnumerable<ProductOfferingSummaryDto> Data,
    int PageNumber, int PageSize, int TotalCount, int TotalPages);
