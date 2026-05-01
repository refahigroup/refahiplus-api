using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.References.Domain.Entities;
using Refahi.Modules.References.Domain.Repositories;

namespace Refahi.Modules.References.Application.Features.Categories.GetCategories;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoriesQueryHandler(ICategoryRepository categoryRepository)
        => _categoryRepository = categoryRepository;

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
            return lookup[request.ParentId].Select(c => MapToDto(c, lookup)).ToList();

        var roots = lookup[null].ToList();
        return roots.Select(c => MapToDto(c, lookup)).ToList();
    }

    private static CategoryDto MapToDto(Category c, ILookup<int?, Category> lookup)
    {
        var childList = lookup[c.Id].ToList();
        List<CategoryDto>? children = childList.Count > 0
            ? childList.Select(ch => MapToDto(ch, lookup)).ToList()
            : null;

        return new CategoryDto(
            c.Id, c.Name, c.Slug, c.CategoryCode,
            c.ImageUrl, c.ParentId, c.SortOrder, c.IsActive,
            children);
    }
}
