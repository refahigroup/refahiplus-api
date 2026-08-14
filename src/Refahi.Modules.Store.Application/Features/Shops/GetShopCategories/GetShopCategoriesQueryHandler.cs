using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Modules.Store.Application.Features.Catalog;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Shops.GetShopCategories;

public sealed class GetShopCategoriesQueryHandler(
    IShopRepository shops,
    IPublicCatalogRepository catalog,
    IMediator mediator,
    IPathService pathService) : IRequestHandler<GetShopCategoriesQuery, List<ShopCategoryDto>>
{
    public async Task<List<ShopCategoryDto>> Handle(GetShopCategoriesQuery request, CancellationToken ct)
    {
        var shop = await shops.GetBySlugAsync(request.ShopSlug, ct);
        if (shop is null)
            return [];
        var atUtc = DateTimeOffset.UtcNow;
        var candidates = await catalog.GetEffectiveCandidatesAsync(
            null, null, shop.Id, null, null, null, null, atUtc, ct);
        var eligible = await PublicCatalogEligibility.FilterAsync(candidates, mediator, atUtc, ct);
        var usedIds = eligible.Select(x => x.CategoryId).ToHashSet();
        if (usedIds.Count == 0)
            return [];
        var categories = Flatten(await mediator.Send(new GetCategoriesQuery(), ct))
            .Where(x => x.IsActive && usedIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .Select(x => new ShopCategoryDto(x.Id, x.Name, x.Slug,
                x.ImageUrl is null ? null : pathService.MakeAbsoluteMediaUrl(x.ImageUrl), x.ParentId))
            .ToList();
        return categories;
    }

    private static IEnumerable<CategoryDto> Flatten(IEnumerable<CategoryDto> source)
    {
        foreach (var category in source)
        {
            yield return category;
            if (category.Children is not null)
                foreach (var child in Flatten(category.Children))
                    yield return child;
        }
    }
}
