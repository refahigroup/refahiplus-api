using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public sealed class FundingContinuationRecoveryWorker(
    IServiceScopeFactory scopes,
    IOptions<FundingContinuationOptions> configured,
    ILogger<FundingContinuationRecoveryWorker> logger) : BackgroundService
{
    private readonly string leaseOwner=$"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(
            Math.Max(5,configured.Value.RecoveryPollSeconds)));
        while(await timer.WaitForNextTickAsync(stoppingToken))
        {
            try{await ProcessBatch(stoppingToken);}
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){ }
            catch(Exception exception){logger.LogError(exception,"Funding continuation recovery batch failed.");}
        }
    }

    private async Task ProcessBatch(CancellationToken ct)
    {
        using var scope=scopes.CreateScope();
        var repository=scope.ServiceProvider.GetRequiredService<IFundingContinuationRepository>();
        var mediator=scope.ServiceProvider.GetRequiredService<IMediator>();
        var now=DateTimeOffset.UtcNow;
        var ids=await repository.LeaseFundingContinuationRecoveryBatchAsync(leaseOwner,now,
            now.AddSeconds(Math.Max(30,configured.Value.ProcessingLeaseSeconds)),
            Math.Clamp(configured.Value.RecoveryBatchSize,1,500),ct);
        foreach(var id in ids)
        {
            try{await mediator.Send(new RecoverFundingContinuationCommand(id),ct);}
            catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(Exception exception){logger.LogWarning(exception,
                "Funding continuation recovery failed. FundingContinuationId={FundingContinuationId}",id);}
        }
    }
}
