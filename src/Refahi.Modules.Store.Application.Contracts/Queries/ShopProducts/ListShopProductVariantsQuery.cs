using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.ShopProducts;

namespace Refahi.Modules.Store.Application.Contracts.Queries.ShopProducts;

public sealed record ListShopProductVariantsQuery(
    Guid ShopId,
    Guid ProductId) : IRequest<IReadOnlyList<ShopProductVariantDto>>;
