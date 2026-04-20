using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Categories;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Categories;

public sealed record GetCategoriesQuery(
    bool IncludeInactive = false,
    int? ModuleId = null,
    int? ParentId = null
) : IRequest<List<CategoryDto>>;
