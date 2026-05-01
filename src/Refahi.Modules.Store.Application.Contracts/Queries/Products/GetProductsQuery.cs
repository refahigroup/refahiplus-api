using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Products;

public sealed record GetProductsQuery(
    int ModuleId,
    Guid? ShopId = null,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<ProductsPagedResponse>;

public sealed record ProductsPagedResponse(
    IEnumerable<ProductSummaryDto> Data,
    int PageNumber, int PageSize, int TotalCount, int TotalPages);
