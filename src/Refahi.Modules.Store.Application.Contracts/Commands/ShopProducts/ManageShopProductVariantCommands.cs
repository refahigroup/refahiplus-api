using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;

public sealed record EnableShopProductVariantCommand(
    Guid ShopId,
    Guid ProductId,
    Guid ProductVariantId) : IRequest<Unit>;

public sealed record DisableShopProductVariantCommand(
    Guid ShopId,
    Guid ProductId,
    Guid ProductVariantId) : IRequest<Unit>;

public sealed record RemoveShopProductVariantCommand(
    Guid ShopId,
    Guid ProductId,
    Guid ProductVariantId) : IRequest<Unit>;
