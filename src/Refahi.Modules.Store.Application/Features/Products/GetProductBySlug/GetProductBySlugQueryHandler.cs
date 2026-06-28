using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Products.GetProductBySlug;

public class GetProductBySlugQueryHandler : IRequestHandler<GetProductBySlugQuery, ProductDetailDto?>
{
    private readonly IProductRepository _productRepo;
    private readonly IShopRepository _shopRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IReviewRepository _reviewRepo;
    private readonly IMediator _mediator;
    private readonly IPathService _pathService;

    public GetProductBySlugQueryHandler(
        IProductRepository productRepo,
        IShopRepository shopRepo,
        IShopProductRepository shopProductRepo,
        IReviewRepository reviewRepo,
        IMediator mediator,
        IPathService pathService)
    {
        _productRepo = productRepo;
        _shopRepo = shopRepo;
        _shopProductRepo = shopProductRepo;
        _reviewRepo = reviewRepo;
        _mediator = mediator;
        _pathService = pathService;
    }

    public async Task<ProductDetailDto?> Handle(GetProductBySlugQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetBySlugAsync(request.Slug, cancellationToken);
        if (product is null || product.IsDeleted)
            return null;

        var ap = await _mediator.Send(new GetAgreementProductByIdQuery(product.AgreementProductId), cancellationToken);
        var salesModel = ap is null ? (SalesModel?)null : (SalesModel)ap.SalesModel;
        var sp = await ResolveShopProductAsync(request, product.Id, cancellationToken);
        if (HasExplicitShopContext(request) && sp is null)
            return null;

        var averageRating = await _reviewRepo.GetAverageRatingAsync(product.Id, cancellationToken);
        var (_, reviewTotal) = await _reviewRepo.GetPagedAsync(product.Id, approvedOnly: true, page: 1, pageSize: 1, cancellationToken);

        var images = product.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(i.Id, _pathService.MakeAbsoluteMediaUrl(i.ImageUrl), i.IsMain, i.SortOrder))
            .ToList();

        var variantAttributes = product.VariantAttributes
            .OrderBy(a => a.SortOrder)
            .Select(a => new VariantAttributeDto(
                a.Id,
                a.Name,
                a.SortOrder,
                a.Values
                    .OrderBy(v => v.SortOrder)
                    .Select(v => new VariantAttributeValueDto(v.Id, v.Value, v.SortOrder))
                    .ToList()))
            .ToList();

        var variants = product.Variants
            .Select(v =>
            {
                var shopPrice = ResolveVariantShopPrice(sp, v);

                return new ProductVariantDto(
                    v.Id, v.SKU,
                    v.ImageUrl is null ? null : _pathService.MakeAbsoluteMediaUrl(v.ImageUrl),
                    v.StockCount,
                    shopPrice.PriceMinor ?? v.PriceMinor, shopPrice.DiscountedPriceMinor ?? v.DiscountedPriceMinor,
                    v.FromDate, v.ToDate, v.CapacityType, v.Capacity, v.RequiresUsageDate,
                    (salesModel.HasValue ? v.IsAvailableFor(salesModel.Value) : v.IsAvailable) && shopPrice.IsActiveInShop,
                    v.Combinations.Select(c =>
                    {
                        var attr = product.VariantAttributes.FirstOrDefault(a => a.Id == c.VariantAttributeId);
                        var val = attr?.Values.FirstOrDefault(vv => vv.Id == c.VariantAttributeValueId);
                        return new VariantCombinationDto(
                            c.VariantAttributeId, attr?.Name ?? string.Empty,
                            c.VariantAttributeValueId, val?.Value ?? string.Empty);
                    }).ToList(),
                    shopPrice.ShopProductVariantId,
                    shopPrice.PriceMinor,
                    shopPrice.DiscountedPriceMinor,
                    shopPrice.PriceSource,
                    shopPrice.IsActiveInShop,
                    shopPrice.UsesShopSpecificPrice);
            })
            .ToList();

        var specifications = product.Specifications
            .OrderBy(s => s.SortOrder)
            .Select(s => new ProductSpecificationDto(s.Id, s.Key, s.Value, s.SortOrder))
            .ToList();

        List<ProductSessionDto>? sessions = null;
        if (ap is not null && (SalesModel)ap.SalesModel == SalesModel.SessionBased)
        {
            sessions = product.Sessions
                .Where(s => s.IsAvailable)
                .Select(s => new ProductSessionDto(
                    s.Id, s.Date.ToString("yyyy-MM-dd"),
                    s.StartTime.ToString("HH:mm"), s.EndTime.ToString("HH:mm"),
                    s.Title, s.Capacity, s.SoldCount, s.RemainingCapacity,
                    s.PriceAdjustment, s.IsActive, s.IsCancelled, s.IsAvailable))
                .ToList();
        }

        return new ProductDetailDto(
            product.Id, product.AgreementProductId,
            product.Title, product.Slug, product.Description,
            sp?.Price ?? 0, sp?.DiscountedPrice ?? 0,
            ap is not null ? ((ProductType)ap.ProductType).ToString() : string.Empty,
            ap is not null ? ((DeliveryType)ap.DeliveryType).ToString() : string.Empty,
            ap is not null ? ((SalesModel)ap.SalesModel).ToString() : string.Empty,
            ap?.CategoryId, null,
            product.IsAvailable, product.StockCount,
            images, variants, variantAttributes, specifications, sessions,
            averageRating, reviewTotal,
            product.CreatedAt);
    }

    private async Task<Refahi.Modules.Store.Domain.Aggregates.ShopProduct?> ResolveShopProductAsync(
        GetProductBySlugQuery request,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var shopId = request.ShopId == Guid.Empty ? null : request.ShopId;
        var shopSlug = string.IsNullOrWhiteSpace(request.ShopSlug)
            ? null
            : request.ShopSlug.Trim().ToLowerInvariant();

        if (shopId.HasValue || shopSlug is not null)
        {
            if (shopSlug is not null)
            {
                var shopBySlug = await _shopRepo.GetBySlugAsync(shopSlug, cancellationToken);
                if (shopBySlug is null || shopBySlug.Status != ShopStatus.Active)
                    return null;

                if (shopId.HasValue && shopBySlug.Id != shopId.Value)
                    throw new ArgumentException("اطلاعات فروشگاه با درخواست محصول هم‌خوانی ندارد.");

                shopId = shopBySlug.Id;
            }
            else if (shopId.HasValue)
            {
                var shopById = await _shopRepo.GetByIdAsync(shopId.Value, cancellationToken);
                if (shopById is null || shopById.Status != ShopStatus.Active)
                    return null;
            }

            var explicitShopProduct = await _shopProductRepo.GetWithVariantOfferingsAsync(
                shopId!.Value,
                productId,
                cancellationToken);

            return explicitShopProduct is { IsActive: true }
                ? explicitShopProduct
                : null;
        }

        // Legacy fallback for older product-detail callers that do not pass shop context.
        var spSummary = (await _shopProductRepo.GetByProductAsync(productId, isActive: true, 1, 1, cancellationToken))
            .Items
            .FirstOrDefault();

        return spSummary is null
            ? null
            : await _shopProductRepo.GetWithVariantOfferingsAsync(spSummary.ShopId, productId, cancellationToken);
    }

    private static bool HasExplicitShopContext(GetProductBySlugQuery request)
        => request.ShopId.HasValue || !string.IsNullOrWhiteSpace(request.ShopSlug);

    private static VariantShopPrice ResolveVariantShopPrice(
        Refahi.Modules.Store.Domain.Aggregates.ShopProduct? shopProduct,
        Refahi.Modules.Store.Domain.Entities.ProductVariant variant)
    {
        var offering = shopProduct?.VariantOfferings
            .FirstOrDefault(o => o.ProductVariantId == variant.Id && !o.IsDeleted);

        if (offering is null)
        {
            return new VariantShopPrice(
                null,
                null,
                null,
                null,
                IsActiveInShop: false,
                UsesShopSpecificPrice: false);
        }

        if (!offering.IsActive)
        {
            return new VariantShopPrice(
                null,
                null,
                offering.Id,
                null,
                IsActiveInShop: false,
                UsesShopSpecificPrice: true);
        }

        return new VariantShopPrice(
            offering.PriceMinor,
            offering.DiscountedPriceMinor,
            offering.Id,
            StorePriceSource.ShopProductVariant.ToString(),
            offering.IsActive,
            UsesShopSpecificPrice: true);
    }

    private sealed record VariantShopPrice(
        long? PriceMinor,
        long? DiscountedPriceMinor,
        Guid? ShopProductVariantId,
        string? PriceSource,
        bool IsActiveInShop,
        bool UsesShopSpecificPrice);
}
