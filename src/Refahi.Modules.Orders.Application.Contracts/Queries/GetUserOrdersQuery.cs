using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Dtos;

namespace Refahi.Modules.Orders.Application.Contracts.Queries;

public sealed record GetUserOrdersQuery(
    Guid UserId,
    int PageNumber,
    int PageSize
) : IRequest<PaginatedOrdersResponse>;

public sealed record PaginatedOrdersResponse(
    IEnumerable<OrderSummaryDto> Data,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
