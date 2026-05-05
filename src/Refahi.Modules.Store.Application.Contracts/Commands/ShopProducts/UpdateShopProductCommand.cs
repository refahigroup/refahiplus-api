using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;

public sealed record UpdateShopProductCommand(
    Guid ShopId,
    Guid ProductId,
    long Price,
    long DiscountedPrice,
    string? Description
) : IRequest<Unit>;
