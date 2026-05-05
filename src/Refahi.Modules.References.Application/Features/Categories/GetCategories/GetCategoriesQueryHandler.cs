using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.References.Domain.Entities;
using Refahi.Modules.References.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.References.Application.Features.Categories.GetCategories;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPathService _pathService;

    public GetCategoriesQueryHandler(ICategoryRepository categoryRepository, IPathService pathService)
    {
        _categoryRepository = categoryRepository;
        _pathService = pathService;
    }

    public async Task<List<CategoryDto>> Handle(
        GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = request.IncludeInactive
            ? await _categoryRepository.GetAllAsync(cancellationToken)
            : await _categoryRepository.GetAllActiveAsync(cancellationToken);

        // Filter by CategoryCode prefix (e.g. "store" matches "store.clothing", "store.food")
        if (!string.IsNullOrWhiteSpace(request.CategoryCodePrefix))
        {
            var prefix = request.CategoryCodePrefix.Trim().ToLowerInvariant();
            categories = categories
                .Where(c => c.CategoryCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var lookup = categories.ToLookup(c => c.ParentId);

        if (request.ParentId.HasValue)
            return lookup[request.ParentId].Select(c => MapToDto(c, lookup, _pathService)).ToList();

        var roots = lookup[null].ToList();
        return roots.Select(c => MapToDto(c, lookup, _pathService)).ToList();
    }

    private static CategoryDto MapToDto(Category c, ILookup<int?, Category> lookup, IPathService pathService)
    {
        var childList = lookup[c.Id].ToList();
        List<CategoryDto>? children = childList.Count > 0
            ? childList.Select(ch => MapToDto(ch, lookup, pathService)).ToList()
            : null;

        return new CategoryDto(
            c.Id, c.Name, c.Slug, c.CategoryCode,
            c.ImageUrl is null ? null : pathService.MakeAbsoluteMediaUrl(c.ImageUrl),
            c.ParentId, c.SortOrder, c.IsActive,
            children);
    }
}
