using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Products.AdminGetProduct;

public class AdminGetProductQueryHandler : IRequestHandler<AdminGetProductQuery, ProductDetailDto?>
{
    private readonly IProductRepository _productRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IReviewRepository _reviewRepo;
    private readonly IMediator _mediator;
    private readonly IPathService _pathService;

    public AdminGetProductQueryHandler(
        IProductRepository productRepo,
        IShopProductRepository shopProductRepo,
        IReviewRepository reviewRepo,
        IMediator mediator,
        IPathService pathService)
    {
        _productRepo = productRepo;
        _shopProductRepo = shopProductRepo;
        _reviewRepo = reviewRepo;
        _mediator = mediator;
        _pathService = pathService;
    }

    public async Task<ProductDetailDto?> Handle(AdminGetProductQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdForAdminAsync(request.ProductId, cancellationToken);
        if (product is null)
            return null;

        var ap = await _mediator.Send(new GetAgreementProductByIdQuery(product.AgreementProductId), cancellationToken);
        var sp = (await _shopProductRepo.GetByProductAsync(product.Id, isActive: null, 1, 1, cancellationToken)).Items.FirstOrDefault();

        var averageRating = await _reviewRepo.GetAverageRatingAsync(product.Id, cancellationToken);
        var (_, reviewTotal) = await _reviewRepo.GetPagedAsync(product.Id, approvedOnly: true, page: 1, pageSize: 1, cancellationToken);

        var images = product.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(i.Id, _pathService.MakeAbsoluteMediaUrl(i.ImageUrl), i.IsMain, i.SortOrder))
            .ToList();

        var variants = product.Variants
            .Select(v => new ProductVariantDto(
                v.Id, v.SKU,
                v.ImageUrl is null ? null : _pathService.MakeAbsoluteMediaUrl(v.ImageUrl),
                v.StockCount,
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
