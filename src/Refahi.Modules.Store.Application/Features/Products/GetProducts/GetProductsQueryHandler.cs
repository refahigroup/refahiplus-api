using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.Store.Application.Features.Products.GetProducts;

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, ProductsPagedResponse>
{
    private readonly IStoreModuleCatalogService _catalog;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IProductRepository _productRepo;
    private readonly IMediator _mediator;

    public GetProductsQueryHandler(
        IStoreModuleCatalogService catalog,
        IShopProductRepository shopProductRepo,
        IProductRepository productRepo,
        IMediator mediator)
    {
        _catalog = catalog;
        _shopProductRepo = shopProductRepo;
        _productRepo = productRepo;
        _mediator = mediator;
    }

    public async Task<ProductsPagedResponse> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var empty = new ProductsPagedResponse([], request.PageNumber, request.PageSize, 0, 0);

        var apIds = await _catalog.GetDisplayableAgreementProductIdsAsync(request.ModuleId, ct);
        if (apIds.Count == 0)
            return empty;

        var (productIds, total) = await _shopProductRepo
            .GetDisplayableProductIdsByAgreementProductIdsAsync(
                apIds, request.ShopId, request.PageNumber, request.PageSize, ct);

        if (productIds.Count == 0)
            return empty;

        var products = await _productRepo.GetByIdsAsync(productIds, ct);
        var productMap = products.ToDictionary(p => p.Id);

        // Batch enrichment — eliminates N+1 cross-module calls
        var usedApIds = products.Select(p => p.AgreementProductId).Distinct().ToList();
        var apDtos = await _mediator.Send(new GetAgreementProductsByIdsQuery(usedApIds), ct);
        var shopProducts = await _shopProductRepo.GetForProductsAsync(productIds, request.ShopId, ct);

        // Preserve pagination order
        var dtos = productIds
            .Where(id => productMap.ContainsKey(id))
            .Select(id =>
            {
                var p = productMap[id];
                apDtos.TryGetValue(p.AgreementProductId, out var ap);
                shopProducts.TryGetValue(p.Id, out var sp);
                var mainImage = p.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                             ?? p.Images.FirstOrDefault()?.ImageUrl;
                return new ProductSummaryDto(
                    p.Id, p.Title, p.Slug,
                    sp?.Price ?? 0,
                    sp?.DiscountedPrice ?? 0,
                    ap is not null ? ((ProductType)ap.ProductType).ToString() : string.Empty,
                    ap is not null ? ((DeliveryType)ap.DeliveryType).ToString() : string.Empty,
                    ap is not null ? ((SalesModel)ap.SalesModel).ToString() : string.Empty,
                    mainImage,
                    p.IsAvailable);
            });

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        return new ProductsPagedResponse(dtos, request.PageNumber, request.PageSize, total, totalPages);
    }
}
