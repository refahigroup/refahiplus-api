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

        return categories.Select(MapToDto).ToList();
    }

    private static CategoryDto MapToDto(StoreCategory c) => new(
        c.Id,
        c.Name,
        c.Slug,
        c.CategoryCode,
        c.ImageUrl,
        c.ParentId,
        c.SortOrder,
        c.IsActive);
}
