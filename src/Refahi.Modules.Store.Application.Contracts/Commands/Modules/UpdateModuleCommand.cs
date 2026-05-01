using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Modules;

public sealed record UpdateModuleCommand(
    int Id,
    string Name,
    string? Description,
    string? IconUrl,
    int SortOrder,
    int? CategoryId = null
) : IRequest<UpdateModuleResponse>;

public sealed record UpdateModuleResponse(int Id, string Name);
