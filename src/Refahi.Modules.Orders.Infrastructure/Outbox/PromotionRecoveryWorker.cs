using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Promotions.Application.Contracts;
namespace Refahi.Modules.Orders.Infrastructure.Outbox;

public sealed class PromotionRecoveryWorker(IServiceScopeFactory scopes, ILogger<PromotionRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                using var scope = scopes.CreateScope(); var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var pending = await scope.ServiceProvider.GetRequiredService<IOrderPromotionOperationStore>().PendingOrderIdsAsync(ct);
                var reservations = await sender.Send(new GetPendingPromotionsQuery(200), ct);
                foreach (var id in pending.Concat(reservations.Select(x => x.OrderId)).Distinct())
                {
                    using var itemScope = scopes.CreateScope();
                    try { await itemScope.ServiceProvider.GetRequiredService<ISender>().Send(new RecoverOrderPromotionCommand(id, reservations.FirstOrDefault(x => x.OrderId == id)?.Id), ct); }
                    catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogError(ex, "Promotion recovery requires retry. OrderId={OrderId}", id); }
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogError(ex, "Promotion recovery scan failed"); }
        }
    }
}
