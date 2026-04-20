using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Modules;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Modules;

public sealed record CreateModuleCommand(
    string Name,
    string Slug,
    string? Description,
    string? IconUrl,
    int SortOrder
) : IRequest<CreateModuleResponse>;

public sealed record CreateModuleResponse(int Id, string Name, string Slug);
