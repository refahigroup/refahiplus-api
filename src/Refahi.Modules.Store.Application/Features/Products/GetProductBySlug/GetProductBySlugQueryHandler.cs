using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.GetProductBySlug;

public class GetProductBySlugQueryHandler : IRequestHandler<GetProductBySlugQuery, ProductDetailDto?>
{
    private readonly IProductRepository _productRepo;
    private readonly IShopRepository _shopRepo;
    private readonly IReviewRepository _reviewRepo;

    public GetProductBySlugQueryHandler(
        IProductRepository productRepo,
        IShopRepository shopRepo,
        IReviewRepository reviewRepo)
    {
        _productRepo = productRepo;
        _shopRepo = shopRepo;
        _reviewRepo = reviewRepo;
    }

    public async Task<ProductDetailDto?> Handle(GetProductBySlugQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetBySlugAsync(request.Slug, cancellationToken);
        if (product is null || product.IsDeleted)
            return null;

        var shop = await _shopRepo.GetByIdAsync(product.ShopId, cancellationToken);

        var averageRating = await _reviewRepo.GetAverageRatingAsync(product.Id, cancellationToken);
        var (reviews, reviewTotal) = await _reviewRepo.GetPagedAsync(product.Id, approvedOnly: true, page: 1, pageSize: 1, cancellationToken);

        var images = product.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(i.Id, i.ImageUrl, i.IsMain, i.SortOrder))
            .ToList();

        var variants = product.Variants
            .Select(v => new ProductVariantDto(
                v.Id,
                v.SKU,
                v.ImageUrl,
                v.StockCount,
                v.PriceMinor,
                v.DiscountedPriceMinor,
                v.IsAvailable,
                v.Combinations.Select(c =>
                {
                    var attr = product.VariantAttributes.FirstOrDefault(a => a.Id == c.VariantAttributeId);
                    var val = attr?.Values.FirstOrDefault(vv => vv.Id == c.VariantAttributeValueId);
                    return new VariantCombinationDto(
                        c.VariantAttributeId,
                        attr?.Name ?? string.Empty,
                        c.VariantAttributeValueId,
                        val?.Value ?? string.Empty);
                }).ToList()))
            .ToList();

        var specifications = product.Specifications
            .OrderBy(s => s.SortOrder)
            .Select(s => new ProductSpecificationDto(s.Id, s.Key, s.Value, s.SortOrder))
            .ToList();

        List<ProductSessionDto>? sessions = null;
        if (product.SalesModel == SalesModel.SessionBased)
        {
            sessions = product.Sessions
                .Where(s => s.IsAvailable)
                .Select(s => new ProductSessionDto(
                    s.Id,
                    s.Date.ToString("yyyy-MM-dd"),
                    s.StartTime.ToString("HH:mm"),
                    s.EndTime.ToString("HH:mm"),
                    s.Title,
                    s.Capacity,
                    s.SoldCount,
                    s.RemainingCapacity,
                    s.PriceAdjustment,
                    s.IsAvailable))
                .ToList();
        }

        return new ProductDetailDto(
            product.Id, product.ShopId,
            product.Title, product.Slug, product.Description,
            product.PriceMinor, product.DiscountedPriceMinor, product.DiscountPercent,
            product.ProductType.ToString(), product.DeliveryType.ToString(), product.SalesModel.ToString(),
            product.CategoryId, product.CategoryCode,
            product.City, product.Area,
            product.IsAvailable, product.StockCount,
            shop?.Name ?? string.Empty,
            shop?.Slug ?? string.Empty,
            images, variants, specifications, sessions,
            averageRating, reviewTotal,
            product.CreatedAt);
    }
}
