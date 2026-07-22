using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Categories;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Categories;

public sealed record GetStoreCategoriesQuery(int ModuleId)
    : IRequest<IReadOnlyList<StoreCategoryDto>>;
