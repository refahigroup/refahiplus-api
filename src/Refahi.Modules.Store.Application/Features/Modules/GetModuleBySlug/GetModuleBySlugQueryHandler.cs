using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Modules;
using Refahi.Modules.Store.Application.Contracts.Queries.Modules;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Modules.GetModuleBySlug;

public class GetModuleBySlugQueryHandler : IRequestHandler<GetModuleBySlugQuery, ModuleDto?>
{
    private readonly IStoreModuleRepository _moduleRepo;
    private readonly IPathService _pathService;

    public GetModuleBySlugQueryHandler(IStoreModuleRepository moduleRepo, IPathService pathService)
    {
        _moduleRepo = moduleRepo;
        _pathService = pathService;
    }

    public async Task<ModuleDto?> Handle(
        GetModuleBySlugQuery request, CancellationToken cancellationToken)
    {
        var module = await _moduleRepo.GetBySlugAsync(request.Slug, cancellationToken);
        if (module is null)
            return null;

        return GetModules.GetModulesQueryHandler.MapToDto(module, _pathService);
    }
}
