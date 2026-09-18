using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public sealed class RefundRecoveryWorker(IServiceScopeFactory scopes,IOptions<RefundOptions> configured,
    ILogger<RefundRecoveryWorker> logger):BackgroundService
{
    private readonly string owner=$"{Environment.MachineName}:refund:{Guid.NewGuid():N}";
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5,configured.Value.RecoveryPollSeconds)));
        while(await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope=scopes.CreateScope();var repo=scope.ServiceProvider.GetRequiredService<IRefundRepository>();var mediator=scope.ServiceProvider.GetRequiredService<IMediator>();var now=DateTimeOffset.UtcNow;
                var ids=await repo.LeaseRefundRecoveryBatchAsync(owner,now,now.AddSeconds(Math.Max(30,configured.Value.LeaseSeconds)),Math.Clamp(configured.Value.BatchSize,1,500),stoppingToken);
                foreach(var id in ids)try{await mediator.Send(new ProcessRefundCommand(id),stoppingToken);}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){throw;}catch(Exception ex){logger.LogWarning(ex,"Refund recovery failed. RefundId={RefundId}",id);}
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){}
            catch(Exception ex){logger.LogError(ex,"Refund recovery batch failed.");}
        }
    }
}
