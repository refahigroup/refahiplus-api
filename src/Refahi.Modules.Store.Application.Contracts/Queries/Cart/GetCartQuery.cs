using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Cart;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Cart;

public sealed record GetCartQuery(Guid UserId) : IRequest<CartDto>;
