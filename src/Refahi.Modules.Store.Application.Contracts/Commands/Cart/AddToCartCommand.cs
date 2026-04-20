using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Cart;

public sealed record AddToCartCommand(
    Guid UserId,
    int ModuleId,
    Guid ProductId,
    Guid? VariantId,
    Guid? SessionId,
    int Quantity
) : IRequest<AddToCartResponse>;

public sealed record AddToCartResponse(Guid CartId, int TotalItems);
