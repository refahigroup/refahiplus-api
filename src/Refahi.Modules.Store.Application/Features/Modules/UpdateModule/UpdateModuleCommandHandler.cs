using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Modules;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Modules.UpdateModule;

public class UpdateModuleCommandHandler : IRequestHandler<UpdateModuleCommand, UpdateModuleResponse>
{
    private readonly IStoreModuleRepository _moduleRepo;

    public UpdateModuleCommandHandler(IStoreModuleRepository moduleRepo)
        => _moduleRepo = moduleRepo;

    public async Task<UpdateModuleResponse> Handle(
        UpdateModuleCommand request, CancellationToken cancellationToken)
    {
        var module = await _moduleRepo.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new StoreDomainException("ماژول یافت نشد", "MODULE_NOT_FOUND");

        module.UpdateInfo(request.Name, request.Description, request.IconUrl, request.SortOrder, request.CategoryId);

        await _moduleRepo.UpdateAsync(module, cancellationToken);

        return new UpdateModuleResponse(module.Id, module.Name);
    }
}
