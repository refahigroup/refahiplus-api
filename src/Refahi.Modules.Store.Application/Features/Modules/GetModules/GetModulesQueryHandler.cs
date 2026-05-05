using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Modules;
using Refahi.Modules.Store.Application.Contracts.Queries.Modules;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Modules.GetModules;

public class GetModulesQueryHandler : IRequestHandler<GetModulesQuery, List<ModuleDto>>
{
    private readonly IStoreModuleRepository _moduleRepo;
    private readonly IPathService _pathService;

    public GetModulesQueryHandler(IStoreModuleRepository moduleRepo, IPathService pathService)
    {
        _moduleRepo = moduleRepo;
        _pathService = pathService;
    }

    public async Task<List<ModuleDto>> Handle(
        GetModulesQuery request, CancellationToken cancellationToken)
    {
        var modules = await _moduleRepo.GetAllAsync(request.IncludeInactive, cancellationToken);
        return modules.Select(m => MapToDto(m, _pathService)).ToList();
    }

    internal static ModuleDto MapToDto(StoreModule m, IPathService pathService) => new(
        m.Id, m.Name, m.Slug, m.Description,
        m.IconUrl is null ? null : pathService.MakeAbsoluteMediaUrl(m.IconUrl),
        m.IsActive, m.SortOrder, m.CategoryId);
}
