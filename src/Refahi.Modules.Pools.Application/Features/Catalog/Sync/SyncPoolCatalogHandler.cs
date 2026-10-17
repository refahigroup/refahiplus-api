using Refahi.Modules.Pools.Application.Features.Catalog.SyncVenueSessions;
using Microsoft.Extensions.Logging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;

namespace Refahi.Modules.Pools.Application.Features.Catalog.Sync;

public sealed class SyncPoolCatalogHandler(IPoolProviderFactory providers, IPoolCatalogRepository repository,
    IPoolMutationLock gate, IOptionsMonitor<PoolsOptions> options, TimeProvider clock, IServiceScopeFactory scopes) : IRequestHandler<SyncPoolCatalogCommand>
{
    public async Task<Unit> Handle(SyncPoolCatalogCommand request, CancellationToken ct)
    {
        foreach (var provider in providers.Enabled())
        {
            if (!request.Venues)
            {
                var pools = await repository.ProviderPoolsAsync(provider.Key, ct);
                await Parallel.ForEachAsync(pools.Where(x => x.IsActive), new ParallelOptions { MaxDegreeOfParallelism = options.CurrentValue.SyncConcurrency, CancellationToken = ct },
                    async (pool, token) =>
                    {
                        using var scope = scopes.CreateScope();
                        try { await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SyncPoolVenueSessionsCommand(pool.Id), token); }
                        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                        catch (Exception ex)
                        {
                            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SyncPoolCatalogHandler>>()
                                .LogWarning("Pool session sync failed. PoolId={PoolId} ErrorType={ErrorType}", pool.Id, ex.GetType().Name);
                        }
                    });
                continue;
            }
            await using var lease = await gate.AcquireAsync(PoolKeys.Lock("catalog:" + provider.Key), ct);
            var observed = new Dictionary<string, PoolSummary>(); var completed = false;
            for (var page = 1; page <= options.CurrentValue.MaxCatalogPages; page++)
            {
                var result = await provider.SearchAsync(new(provider.Key, Page: page, Limit: 50, ActiveOnly: false), ct);
                foreach (var item in result.Items)
                    if (!observed.TryAdd(item.Id, item)) throw new PoolException("فهرست کامل و یکتای تأمین‌کننده دریافت نشد", 502);
                if (result.Items.Count < 50) { completed = true; break; }
            }
            if (!completed) throw new PoolException("فهرست کامل و یکتای تأمین‌کننده دریافت نشد", 502);
            var existing = (await repository.ProviderPoolsAsync(provider.Key, ct)).ToDictionary(x => x.ExternalId);
            foreach (var item in observed.Values)
            {
                if (!existing.TryGetValue(item.Id, out var entry)) { entry = PoolCatalogEntry.Create(provider.Key, item.Id); await repository.AddPoolAsync(entry, ct); }
                entry.Refresh(item.Name, item.State, item.City, item.Area, item.ImageUrl, item.Gender, item.Rating,
                    item.MinimumSalePrice.HasValue ? PoolKeys.Minor(item.MinimumSalePrice.Value, item.PriceUnit) : null, item.IsSelling, clock.GetUtcNow());
            }
            foreach (var old in existing.Values.Where(x => !observed.ContainsKey(x.ExternalId))) old.Deactivate();
            await repository.SaveAsync(ct);
        }
        return Unit.Value;
    }
}
