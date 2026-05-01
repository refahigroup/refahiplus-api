using MediatR;

namespace Refahi.Modules.References.Application.Contracts.Commands;

public sealed record UpdateCategoryCommand(
    int Id,
    string Name,
    string? ImageUrl,
    int SortOrder,
    bool IsActive
) : IRequest<UpdateCategoryResponse>;

public sealed record UpdateCategoryResponse(int Id, string Name, bool IsActive);
