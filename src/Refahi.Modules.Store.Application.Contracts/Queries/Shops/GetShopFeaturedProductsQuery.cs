using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Shops;

public sealed record GetShopFeaturedProductsQuery(
    string ShopSlug,
    int Limit = 12
) : IRequest<List<ShopFeaturedProductDto>>;
