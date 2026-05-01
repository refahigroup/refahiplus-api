using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.Store.Application.Services;

internal sealed class StoreModuleCatalogService : IStoreModuleCatalogService
{
    private readonly IStoreModuleRepository _moduleRepo;
    private readonly IMediator _mediator;
    private readonly IMemoryCache _cache;

    public StoreModuleCatalogService(
        IStoreModuleRepository moduleRepo,
        IMediator mediator,
        IMemoryCache cache)
    {
        _moduleRepo = moduleRepo;
        _mediator = mediator;
        _cache = cache;
    }

    public async Task<IReadOnlyList<Guid>> GetDisplayableAgreementProductIdsAsync(
        int moduleId, CancellationToken ct = default)
    {
        var cacheKey = $"store_module_displayable_apids:{moduleId}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Guid>? cached) && cached is not null)
            return cached;

        var module = await _moduleRepo.GetByIdAsync(moduleId, ct);
        if (module is null || !module.IsActive || module.CategoryId is null)
        {
            // Don't cache empty results — module state may change soon.
            return [];
        }

        var categoryIds = await _mediator.Send(
            new GetCategorySubtreeIdsQuery(module.CategoryId.Value), ct);

        if (categoryIds.Count == 0)
            return [];

        var apIds = await _mediator.Send(
            new GetDisplayableAgreementProductIdsByCategoriesQuery(categoryIds), ct);

        _cache.Set(cacheKey, apIds, TimeSpan.FromMinutes(2));
        return apIds;
    }
}
