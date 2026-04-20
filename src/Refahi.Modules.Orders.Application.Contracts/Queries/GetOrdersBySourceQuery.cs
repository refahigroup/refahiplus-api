using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Queries;

public sealed record GetOrdersBySourceQuery(
    string SourceModule,
    Guid SourceReferenceId,
    int PageNumber,
    int PageSize
) : IRequest<PaginatedOrdersResponse>;
