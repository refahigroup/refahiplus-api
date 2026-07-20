using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Charge.Infrastructure.Observability;

namespace Refahi.Modules.Charge.Infrastructure.Workers;

public sealed class ChargeFulfillmentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ChargeFulfillmentWorker> _logger;

    public ChargeFulfillmentWorker(IServiceScopeFactory scopes, ILogger<ChargeFulfillmentWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Charge fulfillment worker cycle failed.");
            }
        }
    }
    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IChargeRequestRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var items = await repository.GetWorkItemsAsync(DateTime.UtcNow, 20, ct);
        ChargeMetrics.WorkerHeartbeat(nameof(ChargeFulfillmentWorker));
        ChargeMetrics.ReconciliationBatch(items.Count);

        foreach (var item in items)
        {
            try
            {
                await sender.Send(new ReconcileChargeRequestCommand(item.Id), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Charge fulfillment item failed. ChargeRequestId={ChargeRequestId}", item.Id);
            }
        }
    }
}
