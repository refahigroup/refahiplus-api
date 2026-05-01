using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.Store.Application.Features.Products.GetProductBySlug;

public class GetProductBySlugQueryHandler : IRequestHandler<GetProductBySlugQuery, ProductDetailDto?>
{
    private readonly IProductRepository _productRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IReviewRepository _reviewRepo;
    private readonly IMediator _mediator;

    public GetProductBySlugQueryHandler(
        IProductRepository productRepo,
        IShopProductRepository shopProductRepo,
        IReviewRepository reviewRepo,
        IMediator mediator)
    {
        _productRepo = productRepo;
        _shopProductRepo = shopProductRepo;
        _reviewRepo = reviewRepo;
        _mediator = mediator;
    }

    public async Task<ProductDetailDto?> Handle(GetProductBySlugQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetBySlugAsync(request.Slug, cancellationToken);
        if (product is null || product.IsDeleted)
            return null;

        var ap = await _mediator.Send(new GetAgreementProductByIdQuery(product.AgreementProductId), cancellationToken);
        var sp = (await _shopProductRepo.GetByProductAsync(product.Id, isActive: true, 1, 1, cancellationToken)).Items.FirstOrDefault();

        var averageRating = await _reviewRepo.GetAverageRatingAsync(product.Id, cancellationToken);
        var (_, reviewTotal) = await _reviewRepo.GetPagedAsync(product.Id, approvedOnly: true, page: 1, pageSize: 1, cancellationToken);

        var images = product.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(i.Id, i.ImageUrl, i.IsMain, i.SortOrder))
            .ToList();

        var variants = product.Variants
            .Select(v => new ProductVariantDto(
                v.Id, v.SKU, v.ImageUrl, v.StockCount,
                v.PriceMinor, v.DiscountedPriceMinor, v.IsAvailable,
                v.Combinations.Select(c =>
                {
                    var attr = product.VariantAttributes.FirstOrDefault(a => a.Id == c.VariantAttributeId);
                    var val = attr?.Values.FirstOrDefault(vv => vv.Id == c.VariantAttributeValueId);
                    return new VariantCombinationDto(
                        c.VariantAttributeId, attr?.Name ?? string.Empty,
                        c.VariantAttributeValueId, val?.Value ?? string.Empty);
                }).ToList()))
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
                    s.PriceAdjustment, s.IsAvailable))
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
            images, variants, specifications, sessions,
            averageRating, reviewTotal,
            product.CreatedAt);
    }
}
