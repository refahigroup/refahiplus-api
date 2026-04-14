using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Cart;

public sealed record AddToCartCommand(
    Guid UserId,
    Guid ProductId,
    Guid? VariantId,
    Guid? SessionId,
    int Quantity
) : IRequest<AddToCartResponse>;

public sealed record AddToCartResponse(Guid CartId, int TotalItems);
