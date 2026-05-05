using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.References.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.References.Application.Features.Categories.GetCategoryById;

public class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, CategoryDto?>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPathService _pathService;

    public GetCategoryByIdQueryHandler(ICategoryRepository categoryRepository, IPathService pathService)
    {
        _categoryRepository = categoryRepository;
        _pathService = pathService;
    }

    public async Task<CategoryDto?> Handle(
        GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category is null) return null;

        return new CategoryDto(
            category.Id, category.Name, category.Slug, category.CategoryCode,
            category.ImageUrl is null ? null : _pathService.MakeAbsoluteMediaUrl(category.ImageUrl),
            category.ParentId, category.SortOrder, category.IsActive);
    }
}
