using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Shops;

public sealed record GetShopsQuery(
    short? ShopType = null,
    short? Status = null,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<ShopsPagedResponse>;

public sealed record ShopsPagedResponse(
    IEnumerable<ShopSummaryDto> Data,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
