using MediatR;
using Refahi.Modules.Store.Application.Contracts.Queries.ShopProducts;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.GetShopProducts;

public class GetShopProductsQueryHandler : IRequestHandler<GetShopProductsQuery, ShopProductsPagedResponse>
{
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IProductRepository _productRepo;
    private readonly IMediator _mediator;

    public GetShopProductsQueryHandler(
        IShopProductRepository shopProductRepo,
        IProductRepository productRepo,
        IMediator mediator)
    {
        _shopProductRepo = shopProductRepo;
        _productRepo = productRepo;
        _mediator = mediator;
    }

    public async Task<ShopProductsPagedResponse> Handle(GetShopProductsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _shopProductRepo.GetByShopAsync(
            request.ShopId,
            request.IsActive,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        // Batch-fetch all products in a single query to avoid concurrent DbContext operations
        var productIds = items.Select(sp => sp.ProductId).Distinct().ToList();
        var products = await _productRepo.GetByIdsAsync(productIds, cancellationToken);
        var productMap = products.ToDictionary(p => p.Id, p => p);

        // Collect unique AgreementProduct IDs and fetch commission percents in one cross-module call
        var agreementProductIds = productMap.Values
            .Select(p => p.AgreementProductId)
            .Distinct()
            .ToList();

        var commissions = agreementProductIds.Count > 0
            ? await _mediator.Send(new GetCommissionPercentsByAgreementProductIdsQuery(agreementProductIds), cancellationToken)
            : new Dictionary<Guid, decimal>();

        var dtos = items.Select(sp =>
        {
            var product = productMap.GetValueOrDefault(sp.ProductId);
            var mainImage = product?.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                         ?? product?.Images.FirstOrDefault()?.ImageUrl;

            var commissionPercent = product is not null
                && commissions.TryGetValue(product.AgreementProductId, out var pct) ? pct : 0m;
            var commissionPrice = (long)Math.Round(sp.DiscountedPrice * commissionPercent / 100m);

            return new ShopProductDto(
                sp.Id,
                sp.ShopId,
                sp.ProductId,
                product?.Title ?? string.Empty,
                mainImage,
                sp.Price,
                sp.DiscountedPrice,
                commissionPercent,
                commissionPrice,
                sp.Description,
                sp.IsActive,
                sp.IsDeleted,
                sp.CreatedAt);
        }).ToArray();

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        return new ShopProductsPagedResponse(dtos, request.PageNumber, request.PageSize, total, totalPages);
    }
}
