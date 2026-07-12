using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Products.GetProducts;

public sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, ProductsPagedResponse>
{
    private readonly IStoreModuleCatalogService _catalog;
    private readonly IShopProductRepository _shopProductRepository;
    private readonly IMediator _mediator;
    private readonly IPathService _pathService;

    public GetProductsQueryHandler(
        IStoreModuleCatalogService catalog,
        IShopProductRepository shopProductRepository,
        IMediator mediator,
        IPathService pathService)
    {
        _catalog = catalog;
        _shopProductRepository = shopProductRepository;
        _mediator = mediator;
        _pathService = pathService;
    }

    public async Task<ProductsPagedResponse> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var empty = new ProductsPagedResponse([], request.PageNumber, request.PageSize, 0, 0);
        var agreementProductIds = await _catalog.GetDisplayableAgreementProductIdsAsync(request.ModuleId, ct);
        if (agreementProductIds.Count == 0)
            return empty;

        var agreementProducts = await _mediator.Send(
            new GetAgreementProductsByIdsQuery(agreementProductIds), ct);

        var stockBasedIds = agreementProducts.Values
            .Where(x => x.SalesModel == (short)SalesModel.StockBased)
            .Select(x => x.Id)
            .ToList();
        var sessionBasedIds = agreementProducts.Values
            .Where(x => x.SalesModel == (short)SalesModel.SessionBased)
            .Select(x => x.Id)
            .ToList();

        var (offerings, total) = await _shopProductRepository.GetDisplayableVariantOfferingsAsync(
            stockBasedIds,
            sessionBasedIds,
            request.SearchQuery,
            request.Sort,
            request.PageNumber,
            request.PageSize,
            ct);

        var data = offerings.Select(x =>
        {
            agreementProducts.TryGetValue(x.AgreementProductId, out var agreementProduct);
            var discountPercent = x.DiscountedPriceMinor.HasValue
                ? (int?)Math.Round((x.PriceMinor - x.DiscountedPriceMinor.Value) * 100m / x.PriceMinor)
                : null;

            return new ProductOfferingSummaryDto(
                x.ProductId,
                x.ProductVariantId,
                x.ShopProductVariantId,
                x.ShopId,
                x.ProductTitle,
                x.ProductSlug,
                x.VariantLabel,
                x.ShopName,
                x.ShopSlug,
                x.PriceMinor,
                x.DiscountedPriceMinor,
                discountPercent,
                agreementProduct is null ? string.Empty : ((ProductType)agreementProduct.ProductType).ToString(),
                agreementProduct is null ? string.Empty : ((DeliveryType)agreementProduct.DeliveryType).ToString(),
                agreementProduct is null ? string.Empty : ((SalesModel)agreementProduct.SalesModel).ToString(),
                x.ImageUrl is null ? null : _pathService.MakeAbsoluteMediaUrl(x.ImageUrl),
                true);
        }).ToList();

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        return new ProductsPagedResponse(data, request.PageNumber, request.PageSize, total, totalPages);
    }
}
