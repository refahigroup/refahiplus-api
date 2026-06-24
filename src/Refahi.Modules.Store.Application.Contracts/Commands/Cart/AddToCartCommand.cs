using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Cart;

public sealed record AddToCartCommand(
    Guid UserId,
    int ModuleId,
    Guid ShopId,
    Guid ProductId,
    Guid? VariantId,
    Guid? SessionId,
    DateOnly? UsageDate,
    int Quantity
) : IRequest<AddToCartResponse>;

public sealed record AddToCartResponse(Guid CartId, int TotalItems);
