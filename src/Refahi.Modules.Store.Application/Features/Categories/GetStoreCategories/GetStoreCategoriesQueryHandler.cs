using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Dtos.Categories;
using Refahi.Modules.Store.Application.Contracts.Queries.Categories;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Application.Features.Catalog;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Categories.GetStoreCategories;

public sealed class GetStoreCategoriesQueryHandler
    : IRequestHandler<GetStoreCategoriesQuery, IReadOnlyList<StoreCategoryDto>>
{
    private readonly IStoreModuleRepository _moduleRepository;
    private readonly IPublicCatalogRepository _catalog;
    private readonly IMediator _mediator;

    public GetStoreCategoriesQueryHandler(
        IStoreModuleRepository moduleRepository,
        IPublicCatalogRepository catalog,
        IMediator mediator
    )
    {
        _moduleRepository = moduleRepository;
        _catalog = catalog;
        _mediator = mediator;
    }

    public async Task<IReadOnlyList<StoreCategoryDto>> Handle(
        GetStoreCategoriesQuery request,
        CancellationToken ct
    )
    {
        var module = await _moduleRepository.GetByIdAsync(request.ModuleId, ct);
        if (module is null || !module.IsActive || !module.CategoryId.HasValue)
            return [];

        var subtreeIds = await _mediator.Send(
            new GetCategorySubtreeIdsQuery(module.CategoryId.Value), ct);
        var now = DateTimeOffset.UtcNow;
        var coordinates = await _catalog.GetEligibilityCoordinatesAsync(
            subtreeIds, null, null, null, null, now, ct);
        var eligibleCoordinates = await PublicCatalogEligibility.ResolveAsync(
            coordinates, _mediator, now, ct);
        var directlyUsedCategoryIds = eligibleCoordinates.Select(x => x.CategoryId).ToHashSet();

        if (directlyUsedCategoryIds.Count == 0)
            return [];

        var root = await _mediator.Send(new GetCategoryByIdQuery(module.CategoryId.Value), ct);
        if (root is null || !root.IsActive)
            return [];

        var descendants = await _mediator.Send(new GetCategoriesQuery(ParentId: root.Id), ct);
        var categoryById = Flatten(descendants).Append(root).ToDictionary(category => category.Id);

        var includedIds = CollectIncludedIds(root.Id, directlyUsedCategoryIds, categoryById);
        if (!includedIds.Contains(root.Id))
            return [];

        var result = new List<StoreCategoryDto>();
        AddPreorder(root.Id, root.Id, categoryById, includedIds, result);
        return result;
    }

    private static IEnumerable<CategoryDto> Flatten(IEnumerable<CategoryDto> categories)
    {
        foreach (var category in categories)
        {
            yield return category;
            if (category.Children is not null)
            {
                foreach (var child in Flatten(category.Children))
                    yield return child;
            }
        }
    }

    private static HashSet<int> CollectIncludedIds(
        int rootId,
        IEnumerable<int> directlyUsedCategoryIds,
        IReadOnlyDictionary<int, CategoryDto> categoryById
    )
    {
        var included = new HashSet<int>();

        foreach (var categoryId in directlyUsedCategoryIds)
        {
            var path = new List<int>();
            var visited = new HashSet<int>();
            var currentId = categoryId;

            while (visited.Add(currentId) && categoryById.TryGetValue(currentId, out var category))
            {
                path.Add(currentId);
                if (currentId == rootId)
                {
                    included.UnionWith(path);
                    break;
                }

                if (!category.ParentId.HasValue)
                    break;

                currentId = category.ParentId.Value;
            }
        }

        return included;
    }

    private static void AddPreorder(
        int categoryId,
        int rootId,
        IReadOnlyDictionary<int, CategoryDto> categoryById,
        IReadOnlySet<int> includedIds,
        ICollection<StoreCategoryDto> result
    )
    {
        var category = categoryById[categoryId];
        var parent =
            categoryId == rootId || !category.ParentId.HasValue
                ? null
                : categoryById.GetValueOrDefault(category.ParentId.Value);

        result.Add(
            new StoreCategoryDto(
                category.Id,
                category.Name,
                category.Slug,
                category.CategoryCode,
                category.ImageUrl,
                parent?.Id,
                parent?.Name,
                category.SortOrder,
                category.IsActive
            )
        );

        var children = categoryById
            .Values.Where(child => child.ParentId == categoryId && includedIds.Contains(child.Id))
            .OrderBy(child => child.SortOrder)
            .ThenBy(child => child.Name, StringComparer.Ordinal)
            .ThenBy(child => child.Id);

        foreach (var child in children)
            AddPreorder(child.Id, rootId, categoryById, includedIds, result);
    }
}
