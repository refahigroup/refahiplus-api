using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Infrastructure.Workers;

public sealed class PoolCatalogWorker(IServiceScopeFactory scopes, IOptionsMonitor<PoolsOptions> options, TimeProvider clock,
    ILogger<PoolCatalogWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var venuesAt = DateTimeOffset.MinValue; var sessionsAt = DateTimeOffset.MinValue;
        while (!ct.IsCancellationRequested)
        {
            if (options.CurrentValue.CatalogEnabled)
            {
                if (clock.GetUtcNow() - venuesAt >= TimeSpan.FromSeconds(options.CurrentValue.VenueSyncSeconds))
                { if (await Sync(true, ct)) venuesAt = clock.GetUtcNow(); }
                if (clock.GetUtcNow() - sessionsAt >= TimeSpan.FromSeconds(options.CurrentValue.SessionSyncSeconds))
                { if (await Sync(false, ct)) sessionsAt = clock.GetUtcNow(); }
            }
            await Task.Delay(TimeSpan.FromSeconds(5), clock, ct);
        }
    }
    private async Task<bool> Sync(bool venues, CancellationToken ct)
    {
        try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new SyncPoolCatalogCommand(venues), ct); return true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogWarning("Pool catalog sync pending. Venues={Venues} ErrorType={ErrorType}", venues, ex.GetType().Name); return false; }
    }
}

public sealed class PoolRecoveryWorker(IServiceScopeFactory scopes, IOptionsMonitor<PoolsOptions> options, TimeProvider clock,
    ILogger<PoolRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (options.CurrentValue.CatalogEnabled)
            {
                DateTimeOffset? after = null; Guid? afterId = null;
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        using var scope = scopes.CreateScope(); var repo = scope.ServiceProvider.GetRequiredService<IPoolPurchaseRepository>();
                        var ids = await repo.RecoveryIdsAsync(after, afterId, ct); if (ids.Count == 0) break;
                        var last = await repo.GetAsync(ids[^1], ct); after = last!.CreatedAt; afterId = last.Id;
                        foreach (var id in ids)
                        {
                            using var item = scopes.CreateScope();
                            try { await item.ServiceProvider.GetRequiredService<IMediator>().Send(new ReconcilePoolPurchaseCommand(id), ct); }
                            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                            catch (Exception ex) { logger.LogWarning("Pool purchase recovery pending. PurchaseId={PurchaseId} ErrorType={ErrorType}", id, ex.GetType().Name); }
                        }
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { logger.LogWarning("Pool recovery cycle failed. ErrorType={ErrorType}", ex.GetType().Name); }
            }
            await Task.Delay(TimeSpan.FromSeconds(options.CurrentValue.WorkerSeconds), clock, ct);
        }
    }
}
