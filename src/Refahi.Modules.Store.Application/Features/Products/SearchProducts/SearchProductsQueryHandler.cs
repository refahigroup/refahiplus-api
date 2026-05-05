using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Products.SearchProducts;

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, ProductsPagedResponse>
{
    private readonly IStoreModuleCatalogService _catalog;
    private readonly IProductRepository _productRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IMediator _mediator;
    private readonly IPathService _pathService;

    public SearchProductsQueryHandler(
        IStoreModuleCatalogService catalog,
        IProductRepository productRepo,
        IShopProductRepository shopProductRepo,
        IMediator mediator,
        IPathService pathService)
    {
        _catalog = catalog;
        _productRepo = productRepo;
        _shopProductRepo = shopProductRepo;
        _mediator = mediator;
        _pathService = pathService;
    }

    public async Task<ProductsPagedResponse> Handle(SearchProductsQuery request, CancellationToken ct)
    {
        var empty = new ProductsPagedResponse([], request.PageNumber, request.PageSize, 0, 0);

        var apIds = await _catalog.GetDisplayableAgreementProductIdsAsync(request.ModuleId, ct);
        if (apIds.Count == 0)
            return empty;

        var (items, total) = await _productRepo.SearchAsync(
            request.Query, apIds, request.PageNumber, request.PageSize, ct);

        if (items.Count == 0)
            return empty;

        // Batch enrichment — eliminates N+1 cross-module calls
        var productIds = items.Select(p => p.Id).ToList();
        var usedApIds = items.Select(p => p.AgreementProductId).Distinct().ToList();
        var apDtos = await _mediator.Send(new GetAgreementProductsByIdsQuery(usedApIds), ct);
        var shopProducts = await _shopProductRepo.GetForProductsAsync(productIds, shopId: null, ct);

        var dtos = items.Select(p =>
        {
            apDtos.TryGetValue(p.AgreementProductId, out var ap);
            shopProducts.TryGetValue(p.Id, out var sp);
            var mainImage = p.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                         ?? p.Images.FirstOrDefault()?.ImageUrl;
            var mainImageUrl = mainImage is null ? null : _pathService.MakeAbsoluteMediaUrl(mainImage);
            return new ProductSummaryDto(
                p.Id, p.Title, p.Slug,
                sp?.Price ?? 0,
                sp?.DiscountedPrice ?? 0,
                ap is not null ? ((ProductType)ap.ProductType).ToString() : string.Empty,
                ap is not null ? ((DeliveryType)ap.DeliveryType).ToString() : string.Empty,
                ap is not null ? ((SalesModel)ap.SalesModel).ToString() : string.Empty,
                mainImageUrl,
                p.IsAvailable,
                ap?.CommissionPercent ?? 0);
        });

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        return new ProductsPagedResponse(dtos, request.PageNumber, request.PageSize, total, totalPages);
    }
}
