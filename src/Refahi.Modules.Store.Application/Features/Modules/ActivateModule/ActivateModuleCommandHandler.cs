using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Modules;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Modules.ActivateModule;

public class ActivateModuleCommandHandler : IRequestHandler<ActivateModuleCommand, ActivateModuleResponse>
{
    private readonly IStoreModuleRepository _moduleRepo;

    public ActivateModuleCommandHandler(IStoreModuleRepository moduleRepo)
        => _moduleRepo = moduleRepo;

    public async Task<ActivateModuleResponse> Handle(
        ActivateModuleCommand request, CancellationToken cancellationToken)
    {
        var module = await _moduleRepo.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new StoreDomainException("ماژول یافت نشد", "MODULE_NOT_FOUND");

        module.Activate();

        await _moduleRepo.UpdateAsync(module, cancellationToken);

        return new ActivateModuleResponse(module.Id, module.IsActive);
    }
}
