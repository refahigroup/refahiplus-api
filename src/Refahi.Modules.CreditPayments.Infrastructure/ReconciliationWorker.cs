using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public sealed class IntegrityAuditWorker(IServiceScopeFactory scopes, IOptions<ReconciliationOptions> configured,
    ILogger<IntegrityAuditWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configured.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(configured.Value.PollIntervalSeconds));
        do
        {
            foreach (var kind in Enum.GetValues<ReconciliationKind>())
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    await mediator.Send(new ExecuteReconciliationRunCommand((short)kind, null, null, false,
                        $"{Environment.MachineName}-{Environment.ProcessId}"), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogError(ex, "Reconciliation run failed for Kind={Kind}.", kind); }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
