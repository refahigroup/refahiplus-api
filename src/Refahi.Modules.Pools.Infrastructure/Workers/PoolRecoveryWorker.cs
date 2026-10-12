using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Domain;

namespace Refahi.Modules.Pools.Infrastructure.Workers;
public sealed class PoolRecoveryWorker(IServiceScopeFactory scopes, IOptions<PoolsOptions> options, ILogger<PoolRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Value.PurchaseEnabled) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                DateTimeOffset? afterCreated = null; Guid? afterId = null;
                while (!ct.IsCancellationRequested)
                {
                    using var batch = scopes.CreateScope(); var repository = batch.ServiceProvider.GetRequiredService<IPoolBookingRepository>();
                    var ids = await repository.CandidatesAsync(afterCreated, afterId, ct); if (ids.Count == 0) break;
                    var last = await repository.GetAsync(ids[^1], ct); afterCreated = last!.CreatedAt; afterId = last.Id;
                    foreach (var id in ids)
                    {
                        using var item = scopes.CreateScope();
                        try { await item.ServiceProvider.GetRequiredService<IMediator>().Send(new ReconcilePoolBookingCommand(id), ct); }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                        catch (Exception ex) { logger.LogWarning("Pool recovery pending. BookingId={BookingId} ErrorType={ErrorType}", id, ex.GetType().Name); }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning("Pool recovery cycle failed. ErrorType={ErrorType}", ex.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.WorkerSeconds), ct);
        }
    }
}
