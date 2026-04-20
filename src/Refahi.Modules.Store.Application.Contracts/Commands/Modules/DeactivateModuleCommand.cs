using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Modules;

public sealed record DeactivateModuleCommand(int Id) : IRequest<DeactivateModuleResponse>;

public sealed record DeactivateModuleResponse(int Id, bool IsActive);
