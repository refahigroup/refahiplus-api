using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Products;

public sealed record AdminGetProductsQuery(
    int? CategoryId = null,
    Guid? ShopId = null,
    bool? IsDeleted = null,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<AdminProductsPagedResponse>;

public sealed record AdminProductsPagedResponse(
    IEnumerable<ProductSummaryDto> Data,
    int PageNumber, int PageSize, int TotalCount, int TotalPages);
