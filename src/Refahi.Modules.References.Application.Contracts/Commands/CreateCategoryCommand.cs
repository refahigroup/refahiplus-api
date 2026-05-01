using MediatR;

namespace Refahi.Modules.References.Application.Contracts.Commands;

public sealed record CreateCategoryCommand(
    string Name,
    string Slug,
    string CategoryCode,
    string? ImageUrl,
    int? ParentId,
    int SortOrder
) : IRequest<CreateCategoryResponse>;

public sealed record CreateCategoryResponse(int Id, string Name, string Slug, string CategoryCode);
