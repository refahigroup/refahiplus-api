using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.ShopProducts;
using Refahi.Modules.Store.Application.Contracts.Queries.ShopProducts;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.ShopProductVariants;

public class ListShopProductVariantsQueryHandler
    : IRequestHandler<ListShopProductVariantsQuery, IReadOnlyList<ShopProductVariantDto>>
{
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IProductRepository _productRepo;

    public ListShopProductVariantsQueryHandler(
        IShopProductRepository shopProductRepo,
        IProductRepository productRepo)
    {
        _shopProductRepo = shopProductRepo;
        _productRepo = productRepo;
    }

    public async Task<IReadOnlyList<ShopProductVariantDto>> Handle(
        ListShopProductVariantsQuery request,
        CancellationToken cancellationToken)
    {
        var shopProduct = await _shopProductRepo.GetWithVariantOfferingsAsync(
            request.ShopId,
            request.ProductId,
            cancellationToken)
            ?? throw new StoreDomainException("محصول در این فروشگاه یافت نشد", "SHOP_PRODUCT_NOT_FOUND");

        var product = await _productRepo.GetByIdForAdminAsync(request.ProductId, cancellationToken);

        return shopProduct.VariantOfferings
            .Where(v => !v.IsDeleted)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => ShopProductVariantMapper.ToDto(v, product))
            .ToArray();
    }
}
