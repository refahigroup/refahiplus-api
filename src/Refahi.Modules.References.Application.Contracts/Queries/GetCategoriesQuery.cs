using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;

namespace Refahi.Modules.References.Application.Contracts.Queries;

public sealed record GetCategoriesQuery(
    string? CategoryCodePrefix = null,
    int? ParentId = null,
    bool IncludeInactive = false
) : IRequest<List<CategoryDto>>;
