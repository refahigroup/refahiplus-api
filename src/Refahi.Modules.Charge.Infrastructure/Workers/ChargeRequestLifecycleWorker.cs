using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;

namespace Refahi.Modules.Charge.Infrastructure.Workers;

public sealed class ChargeRequestLifecycleWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ChargeRequestLifecycleWorker> _logger;
    public ChargeRequestLifecycleWorker(IServiceScopeFactory scopes, ILogger<ChargeRequestLifecycleWorker> logger)
    { _scopes = scopes; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await ProcessAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { _logger.LogError(ex, "Charge lifecycle worker cycle failed."); }
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IChargeRequestRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var items = await repository.GetExpiredCandidatesAsync(DateTime.UtcNow, 50, ct);

        foreach (var request in items)
        {
            try
            {
                if (request.Status == ChargeRequestStatus.ConvertedToOrder && request.OrderId.HasValue)
                {
                    var order = await sender.Send(new GetOrderByIdQuery(request.OrderId.Value, Guid.Empty, "Admin"), ct);
                    if (order is null) continue;

                    if (order.PaymentState is "Unpaid" or "Reserved")
                    {
                        await sender.Send(new CancelOrderCommand(request.OrderId.Value,
                            "مهلت پرداخت سفارش شارژ به پایان رسیده است", $"charge-expire-{request.Id:N}"), ct);
                    }
                    else if (order.PaymentState is not "Released" || order.Status is not "Cancelled")
                    {
                        continue;
                    }
                }

                request.MarkExpiredAfterOrderClosed(DateTime.UtcNow);
                await repository.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Charge request expiration failed. ChargeRequestId={ChargeRequestId} OrderId={OrderId}",
                    request.Id, request.OrderId);
            }
        }
    }
}
