using MediatR;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Shops.GetShopCategories;

public class GetShopCategoriesQueryHandler : IRequestHandler<GetShopCategoriesQuery, List<ShopCategoryDto>>
{
    private readonly IShopRepository _shopRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IProductRepository _productRepo;
    private readonly IMediator _mediator;
    private readonly IPathService _pathService;

    public GetShopCategoriesQueryHandler(
        IShopRepository shopRepo,
        IShopProductRepository shopProductRepo,
        IProductRepository productRepo,
        IMediator mediator,
        IPathService pathService)
    {
        _shopRepo = shopRepo;
        _shopProductRepo = shopProductRepo;
        _productRepo = productRepo;
        _mediator = mediator;
        _pathService = pathService;
    }

    public async Task<List<ShopCategoryDto>> Handle(GetShopCategoriesQuery request, CancellationToken ct)
    {
        var shop = await _shopRepo.GetBySlugAsync(request.ShopSlug, ct);
        if (shop is null) return new();

        const int batchSize = 500;
        var page = 1;
        var productIds = new HashSet<Guid>();

        while (true)
        {
            var (items, total) = await _shopProductRepo.GetByShopAsync(shop.Id, isActive: true, page, batchSize, ct);
            foreach (var sp in items)
                productIds.Add(sp.ProductId);

            if (items.Count < batchSize || productIds.Count >= total)
                break;
            page++;
        }

        if (productIds.Count == 0) return new();

        var products = await _productRepo.GetByIdsAsync(productIds.ToList(), ct);
        var apIds = products
            .Where(p => !p.IsDeleted && p.IsAvailable)
            .Select(p => p.AgreementProductId)
            .Distinct()
            .ToList();

        if (apIds.Count == 0) return new();

        var apMap = await _mediator.Send(new GetAgreementProductsByIdsQuery(apIds), ct);
        var categoryIds = apMap.Values
            .Where(ap => ap.CategoryId.HasValue)
            .Select(ap => ap.CategoryId!.Value)
            .Distinct()
            .ToList();

        if (categoryIds.Count == 0) return new();

        var result = new List<ShopCategoryDto>();
        foreach (var categoryId in categoryIds)
        {
            var category = await _mediator.Send(new GetCategoryByIdQuery(categoryId), ct);
            if (category is null || !category.IsActive) continue;

            result.Add(new ShopCategoryDto(
                category.Id,
                category.Name,
                category.Slug,
                category.ImageUrl is null ? null : _pathService.MakeAbsoluteMediaUrl(category.ImageUrl),
                category.ParentId));
        }

        return result.OrderBy(c => c.Name).ToList();
    }
}
