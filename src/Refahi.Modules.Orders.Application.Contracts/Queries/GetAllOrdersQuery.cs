using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Queries;

public sealed record GetAllOrdersQuery(
    int PageNumber,
    int PageSize,
    string? Status,
    Guid? UserId,
    string? SourceModule
) : IRequest<PaginatedOrdersResponse>;
