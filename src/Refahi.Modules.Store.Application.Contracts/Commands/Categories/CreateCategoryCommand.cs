using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Categories;

public sealed record CreateCategoryCommand(
    int ModuleId,
    string Name,
    string Slug,
    string CategoryCode,
    string? ImageUrl,
    int? ParentId,
    int SortOrder
) : IRequest<CreateCategoryResponse>;

public sealed record CreateCategoryResponse(int Id, string Name, string Slug, string CategoryCode);
