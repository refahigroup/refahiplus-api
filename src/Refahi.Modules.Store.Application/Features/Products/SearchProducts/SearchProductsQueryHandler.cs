using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.SearchProducts;

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, ProductsPagedResponse>
{
    private readonly IProductRepository _productRepo;
    private readonly IShopRepository _shopRepo;

    public SearchProductsQueryHandler(IProductRepository productRepo, IShopRepository shopRepo)
    {
        _productRepo = productRepo;
        _shopRepo = shopRepo;
    }

    public async Task<ProductsPagedResponse> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _productRepo.SearchAsync(
            request.Query,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var shopIds = items.Select(p => p.ShopId).Distinct().ToList();
        var shops = new Dictionary<Guid, Shop>();
        foreach (var shopId in shopIds)
        {
            var shop = await _shopRepo.GetByIdAsync(shopId, cancellationToken);
            if (shop is not null)
                shops[shopId] = shop;
        }

        var dtos = items.Select(p =>
        {
            var mainImage = p.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                         ?? p.Images.FirstOrDefault()?.ImageUrl;
            shops.TryGetValue(p.ShopId, out var shop);
            return new ProductSummaryDto(
                p.Id, p.Title, p.Slug,
                p.PriceMinor, p.DiscountedPriceMinor, p.DiscountPercent,
                p.ProductType.ToString(), p.DeliveryType.ToString(), p.SalesModel.ToString(),
                mainImage,
                shop?.Name ?? string.Empty,
                p.City,
                p.IsAvailable);
        });

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        return new ProductsPagedResponse(dtos, request.PageNumber, request.PageSize, total, totalPages);
    }
}
