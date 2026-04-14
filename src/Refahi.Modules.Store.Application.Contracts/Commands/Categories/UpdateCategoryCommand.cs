using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Categories;

public sealed record UpdateCategoryCommand(
    int Id,
    string Name,
    string? ImageUrl,
    int SortOrder,
    bool IsActive
) : IRequest<UpdateCategoryResponse>;

public sealed record UpdateCategoryResponse(int Id, string Name, bool IsActive);
