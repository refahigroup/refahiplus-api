using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Modules;

public sealed record ActivateModuleCommand(int Id) : IRequest<ActivateModuleResponse>;

public sealed record ActivateModuleResponse(int Id, bool IsActive);
