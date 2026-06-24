using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.ShopProducts;

namespace Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;

public sealed record UpsertShopProductVariantCommand(
    Guid ShopId,
    Guid ProductId,
    Guid ProductVariantId,
    long PriceMinor,
    long? DiscountedPriceMinor,
    bool IsActive) : IRequest<ShopProductVariantDto>;
