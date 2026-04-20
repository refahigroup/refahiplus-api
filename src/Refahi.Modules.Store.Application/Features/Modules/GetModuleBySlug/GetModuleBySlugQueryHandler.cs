using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Modules;
using Refahi.Modules.Store.Application.Contracts.Queries.Modules;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Modules.GetModuleBySlug;

public class GetModuleBySlugQueryHandler : IRequestHandler<GetModuleBySlugQuery, ModuleDto?>
{
    private readonly IStoreModuleRepository _moduleRepo;

    public GetModuleBySlugQueryHandler(IStoreModuleRepository moduleRepo)
        => _moduleRepo = moduleRepo;

    public async Task<ModuleDto?> Handle(
        GetModuleBySlugQuery request, CancellationToken cancellationToken)
    {
        var module = await _moduleRepo.GetBySlugAsync(request.Slug, cancellationToken);
        if (module is null)
            return null;

        return GetModules.GetModulesQueryHandler.MapToDto(module);
    }
}
