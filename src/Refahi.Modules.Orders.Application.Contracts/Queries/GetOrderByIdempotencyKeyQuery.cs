using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Dtos;

namespace Refahi.Modules.Orders.Application.Contracts.Queries;

public sealed record GetOrderByIdempotencyKeyQuery(
    string IdempotencyKey,
    Guid CallerUserId,
    string SourceModule
) : IRequest<OrderDto?>;
