using MediatR;
using Refahi.Modules.References.Application.Contracts.Commands;
using Refahi.Modules.References.Domain.Entities;
using Refahi.Modules.References.Domain.Exceptions;
using Refahi.Modules.References.Domain.Repositories;

namespace Refahi.Modules.References.Application.Features.Categories.CreateCategory;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CreateCategoryResponse>
{
    private readonly ICategoryRepository _categoryRepository;

    public CreateCategoryCommandHandler(ICategoryRepository categoryRepository)
        => _categoryRepository = categoryRepository;

    public async Task<CreateCategoryResponse> Handle(
        CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var slugExists = await _categoryRepository.SlugExistsAsync(
            request.Slug.Trim().ToLowerInvariant(), ct: cancellationToken);
        if (slugExists)
            throw new ReferencesDomainException("این اسلاگ قبلاً ثبت شده است", "SLUG_ALREADY_EXISTS");

        if (request.ParentId.HasValue)
        {
            var parent = await _categoryRepository.GetByIdAsync(request.ParentId.Value, cancellationToken);
            if (parent is null)
                throw new ReferencesDomainException("دسته‌بندی والد یافت نشد", "PARENT_CATEGORY_NOT_FOUND");
        }

        var category = Category.Create(
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
