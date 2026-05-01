using MediatR;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.References.Domain.Repositories;

namespace Refahi.Modules.References.Application.Features.Categories.GetCategorySubtreeIds;

public class GetCategorySubtreeIdsQueryHandler
    : IRequestHandler<GetCategorySubtreeIdsQuery, IReadOnlyList<int>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategorySubtreeIdsQueryHandler(ICategoryRepository categoryRepository)
        => _categoryRepository = categoryRepository;

    public Task<IReadOnlyList<int>> Handle(
        GetCategorySubtreeIdsQuery request, CancellationToken cancellationToken)
        => _categoryRepository.GetSubtreeIdsAsync(request.RootCategoryId, cancellationToken);
}
