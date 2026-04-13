using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Dtos;

namespace Refahi.Modules.Orders.Application.Contracts.Queries;

public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<OrderDto?>;
