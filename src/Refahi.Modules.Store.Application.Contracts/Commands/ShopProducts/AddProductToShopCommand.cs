using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;

public sealed record AddProductToShopCommand(
    Guid ShopId,
    Guid ProductId,
    long Price,
    long DiscountedPrice,
    string? Description
) : IRequest<AddProductToShopResponse>;

public sealed record AddProductToShopResponse(Guid Id);
