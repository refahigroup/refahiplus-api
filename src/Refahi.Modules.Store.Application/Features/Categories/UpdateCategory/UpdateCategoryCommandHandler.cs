using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Categories;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Categories.UpdateCategory;

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, UpdateCategoryResponse>
{
    private readonly IStoreCategoryRepository _categoryRepository;

    public UpdateCategoryCommandHandler(IStoreCategoryRepository categoryRepository)
        => _categoryRepository = categoryRepository;

    public async Task<UpdateCategoryResponse> Handle(
        UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new StoreDomainException("دسته‌بندی یافت نشد", "CATEGORY_NOT_FOUND");

        category.UpdateInfo(request.Name, request.ImageUrl, request.SortOrder);

        if (request.IsActive && !category.IsActive)
            category.Activate();
        else if (!request.IsActive && category.IsActive)
            category.Deactivate();

        await _categoryRepository.UpdateAsync(category, cancellationToken);

        return new UpdateCategoryResponse(category.Id, category.Name, category.IsActive);
    }
}
