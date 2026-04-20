using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Categories;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Categories.CreateCategory;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CreateCategoryResponse>
{
    private readonly IStoreCategoryRepository _categoryRepository;

    public CreateCategoryCommandHandler(IStoreCategoryRepository categoryRepository)
        => _categoryRepository = categoryRepository;

    public async Task<CreateCategoryResponse> Handle(
        CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        // Validate slug uniqueness
        var existing = await _categoryRepository.GetAllActiveAsync(cancellationToken);
        if (existing.Any(c => c.Slug == request.Slug.Trim().ToLower()))
            throw new StoreDomainException("این اسلاگ قبلاً ثبت شده است", "SLUG_ALREADY_EXISTS");

        // Validate parent exists if provided
        if (request.ParentId.HasValue)
        {
            var parent = await _categoryRepository.GetByIdAsync(request.ParentId.Value, cancellationToken);
            if (parent is null)
                throw new StoreDomainException("دسته‌بندی والد یافت نشد", "PARENT_CATEGORY_NOT_FOUND");
        }

        var category = StoreCategory.Create(
            request.ModuleId,
            request.Name,
            request.Slug,
            request.CategoryCode,
            request.ImageUrl,
            request.ParentId,
            request.SortOrder);

        await _categoryRepository.AddAsync(category, cancellationToken);

        return new CreateCategoryResponse(category.Id, category.Name, category.Slug, category.CategoryCode);
    }
}
