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
    private readonly IStoreModuleCatalogService _catalog;
    private readonly TimeProvider _timeProvider;

    public GetProductBySlugQueryHandler(
        IProductRepository productRepo,
        IShopRepository shopRepo,
        IShopProductRepository shopProductRepo,
        IReviewRepository reviewRepo,
        IMediator mediator,
        IPathService pathService,
        IStoreModuleCatalogService catalog,
        TimeProvider timeProvider)
    {
        _productRepo = productRepo;
        _shopRepo = shopRepo;
        _shopProductRepo = shopProductRepo;
        _reviewRepo = reviewRepo;
        _mediator = mediator;
        _pathService = pathService;
        _catalog = catalog;
        _timeProvider = timeProvider;
    }

    public async Task<ProductDetailDto?> Handle(GetProductBySlugQuery request, CancellationToken cancellationToken)
    {
        var allowedAgreementProductIds = await _catalog.GetDisplayableAgreementProductIdsAsync(
            request.ModuleId,
            cancellationToken);
        var product = await _productRepo.GetDisplayableBySlugAsync(
            request.Slug,
            allowedAgreementProductIds,
            cancellationToken);
        if (product is null)
            return null;

        var ap = await _mediator.Send(new GetAgreementProductByIdQuery(product.AgreementProductId), cancellationToken);
        if (ap is null)
            return null;

        var salesModel = (SalesModel)ap.SalesModel;
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var sp = await ResolveShopProductAsync(request, product.Id, salesModel, today, cancellationToken);
        if (sp is null)
            return null;

        var availableSessions = salesModel == SalesModel.SessionBased
            ? product.Sessions.Where(s => s.Date >= today && s.IsAvailable).ToList()
            : [];
        if (salesModel == SalesModel.SessionBased && availableSessions.Count == 0)
            return null;

        var averageRating = await _reviewRepo.GetAverageRatingAsync(product.Id, cancellationToken);
        var (_, reviewTotal) = await _reviewRepo.GetPagedAsync(product.Id, approvedOnly: true, page: 1, pageSize: 1, cancellationToken);

        var images = product.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(i.Id, _pathService.MakeAbsoluteMediaUrl(i.ImageUrl), i.IsMain, i.SortOrder))
            .ToList();

        var displayableVariants = product.Variants
            .Select(variant => new
            {
                Variant = variant,
                ShopPrice = ResolveVariantShopPrice(sp, variant)
            })
            .Where(x => x.ShopPrice is not null && IsVariantDisplayable(x.Variant, salesModel))
            .Select(x => (x.Variant, ShopPrice: x.ShopPrice!))
            .ToList();

        if (displayableVariants.Count == 0)
            return null;

        var usedCombinations = displayableVariants
            .SelectMany(x => x.Variant.Combinations)
            .ToList();
        var usedAttributeIds = usedCombinations.Select(c => c.VariantAttributeId).ToHashSet();
        var usedValueIds = usedCombinations.Select(c => c.VariantAttributeValueId).ToHashSet();

        var variantAttributes = product.VariantAttributes
            .Where(a => usedAttributeIds.Contains(a.Id))
            .OrderBy(a => a.SortOrder)
            .Select(a => new VariantAttributeDto(
                a.Id,
                a.Name,
                a.SortOrder,
                a.Values
                    .Where(v => usedValueIds.Contains(v.Id))
                    .OrderBy(v => v.SortOrder)
                    .Select(v => new VariantAttributeValueDto(v.Id, v.Value, v.SortOrder))
                    .ToList()))
            .ToList();

        var variants = displayableVariants
            .Select(x =>
            {
                var v = x.Variant;
                var shopPrice = x.ShopPrice;

                return new ProductVariantDto(
                    v.Id, v.SKU,
                    v.ImageUrl is null ? null : _pathService.MakeAbsoluteMediaUrl(v.ImageUrl),
                    v.StockCount,
                    shopPrice.PriceMinor, shopPrice.DiscountedPriceMinor,
                    v.FromDate, v.ToDate, v.CapacityType, v.Capacity, v.RequiresUsageDate,
                    true,
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
        if (salesModel == SalesModel.SessionBased)
        {
            sessions = availableSessions
                .Select(s => new ProductSessionDto(
                    s.Id, s.Date.ToString("yyyy-MM-dd"),
                    s.StartTime.ToString("HH:mm"), s.EndTime.ToString("HH:mm"),
                    s.Title, s.Capacity, s.SoldCount, s.RemainingCapacity,
                    s.PriceAdjustment, s.IsActive, s.IsCancelled, s.IsAvailable))
                .ToList();
        }

        var representativePrice = displayableVariants
            .OrderBy(x => x.ShopPrice.DiscountedPriceMinor ?? x.ShopPrice.PriceMinor)
            .ThenByDescending(x => x.ShopPrice.OfferingCreatedAt)
            .ThenBy(x => x.ShopPrice.ShopProductVariantId)
            .First()
            .ShopPrice;

        return new ProductDetailDto(
            product.Id, product.AgreementProductId,
            product.Title, product.Slug, product.Description,
            representativePrice.PriceMinor, representativePrice.DiscountedPriceMinor ?? 0,
            ((ProductType)ap.ProductType).ToString(),
            ((DeliveryType)ap.DeliveryType).ToString(),
            salesModel.ToString(),
            ap.CategoryId, null,
            product.IsAvailable, product.StockCount,
            images, variants, variantAttributes, specifications, sessions,
            averageRating, reviewTotal,
            product.CreatedAt);
    }

    private async Task<Refahi.Modules.Store.Domain.Aggregates.ShopProduct?> ResolveShopProductAsync(
        GetProductBySlugQuery request,
        Guid productId,
        SalesModel salesModel,
        DateOnly today,
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

        // Legacy fallback for callers without shop context: use the shop carrying the
        // cheapest currently displayable variant.
        return await _shopProductRepo.GetBestDisplayableForProductAsync(
            productId,
            salesModel,
            today,
            cancellationToken);
    }

    private static bool IsVariantDisplayable(
        Refahi.Modules.Store.Domain.Entities.ProductVariant variant,
        SalesModel salesModel)
        => salesModel switch
        {
            SalesModel.StockBased => variant.IsAvailable && variant.StockCount > 0,
            SalesModel.SessionBased => variant.IsAvailableFor(salesModel),
            _ => false
        };

    private static VariantShopPrice? ResolveVariantShopPrice(
        Refahi.Modules.Store.Domain.Aggregates.ShopProduct? shopProduct,
        Refahi.Modules.Store.Domain.Entities.ProductVariant variant)
    {
        var offering = shopProduct?.VariantOfferings
            .FirstOrDefault(o => o.ProductVariantId == variant.Id && !o.IsDeleted);

        if (offering is null
            || !offering.IsActive
            || offering.PriceMinor <= 0
            || offering.DiscountedPriceMinor is <= 0
            || offering.DiscountedPriceMinor >= offering.PriceMinor)
            return null;

        return new VariantShopPrice(
            offering.PriceMinor,
            offering.DiscountedPriceMinor,
            offering.Id,
            StorePriceSource.ShopProductVariant.ToString(),
            IsActiveInShop: true,
            UsesShopSpecificPrice: true,
            offering.CreatedAt);
    }

    private sealed record VariantShopPrice(
        long PriceMinor,
        long? DiscountedPriceMinor,
        Guid ShopProductVariantId,
        string? PriceSource,
        bool IsActiveInShop,
        bool UsesShopSpecificPrice,
        DateTimeOffset OfferingCreatedAt);
}
