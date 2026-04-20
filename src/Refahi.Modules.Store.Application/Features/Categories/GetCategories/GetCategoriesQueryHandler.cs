using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Categories;
using Refahi.Modules.Store.Application.Contracts.Queries.Categories;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Categories.GetCategories;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, List<CategoryDto>>
{
    private readonly IStoreCategoryRepository _categoryRepository;

    public GetCategoriesQueryHandler(IStoreCategoryRepository categoryRepository)
        => _categoryRepository = categoryRepository;

    public async Task<List<CategoryDto>> Handle(
        GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = request.IncludeInactive
            ? await _categoryRepository.GetAllAsync(cancellationToken)
            : await _categoryRepository.GetAllActiveAsync(cancellationToken);

        if (request.ModuleId.HasValue)
            categories = categories.Where(c => c.ModuleId == request.ModuleId.Value).ToList();

        // Build parent-only list (top level) and attach children
        var lookup = categories.ToLookup(c => c.ParentId);
        var roots = lookup[null].ToList();

        if (request.ParentId.HasValue)
        {
            // Return only children of the specified parent
            return lookup[request.ParentId].Select(c => MapToDto(c, lookup)).ToList();
        }

        return roots.Select(c => MapToDto(c, lookup)).ToList();
    }

    private static CategoryDto MapToDto(StoreCategory c, ILookup<int?, StoreCategory>? lookup = null)
    {
        List<CategoryDto>? children = null;
        if (lookup is not null)
        {
            var childList = lookup[c.Id].ToList();
            if (childList.Count > 0)
                children = childList.Select(ch => MapToDto(ch, null)).ToList();
        }

        return new CategoryDto(
            c.Id,
            c.ModuleId,
            c.Name,
            c.Slug,
            c.CategoryCode,
            c.ImageUrl,
            c.ParentId,
            c.SortOrder,
            c.IsActive,
            children);
    }
}
