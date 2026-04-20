using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Modules;
using Refahi.Modules.Store.Application.Contracts.Queries.Modules;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Modules.GetModules;

public class GetModulesQueryHandler : IRequestHandler<GetModulesQuery, List<ModuleDto>>
{
    private readonly IStoreModuleRepository _moduleRepo;

    public GetModulesQueryHandler(IStoreModuleRepository moduleRepo)
        => _moduleRepo = moduleRepo;

    public async Task<List<ModuleDto>> Handle(
        GetModulesQuery request, CancellationToken cancellationToken)
    {
        var modules = await _moduleRepo.GetAllAsync(request.IncludeInactive, cancellationToken);
        return modules.Select(MapToDto).ToList();
    }

    internal static ModuleDto MapToDto(StoreModule m) => new(
        m.Id, m.Name, m.Slug, m.Description, m.IconUrl, m.IsActive, m.SortOrder);
}
