using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public sealed class AutomaticPaymentResumeWorker(
    IServiceScopeFactory scopes,
    IOptions<PaymentExecutionOptions> configured,
    ILogger<AutomaticPaymentResumeWorker> logger) : BackgroundService
{
    private readonly string leaseOwner=$"{Environment.MachineName}:auto:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5,configured.Value.AutoResumePollSeconds)));
        while(await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope=scopes.CreateScope();
                var repository=scope.ServiceProvider.GetRequiredService<IPaymentExecutionRepository>();
                var mediator=scope.ServiceProvider.GetRequiredService<IMediator>();
                var now=DateTimeOffset.UtcNow;
                var ids=await repository.LeaseAutomaticResumeBatchAsync(leaseOwner,now,
                    now.AddSeconds(Math.Max(30,configured.Value.ProcessingLeaseSeconds)),
                    Math.Clamp(configured.Value.RecoveryBatchSize,1,500),stoppingToken);
                foreach(var id in ids)
                {
                    try{await mediator.Send(new ExecuteGatewayPaymentCommand(id,"FundingAutoResume"),stoppingToken);}
                    catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){throw;}
                    catch(Exception ex){logger.LogWarning(ex,"Automatic payment resume failed. GatewayPaymentId={GatewayPaymentId}",id);}
                }
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){ }
            catch(Exception ex){logger.LogError(ex,"Automatic payment resume batch failed.");}
        }
    }
}

public sealed class PaymentRecoveryWorker(
    IServiceScopeFactory scopes,
    IOptions<PaymentExecutionOptions> configured,
    ILogger<PaymentRecoveryWorker> logger) : BackgroundService
{
    private readonly string leaseOwner=$"{Environment.MachineName}:payment:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5,configured.Value.RecoveryPollSeconds)));
        while(await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope=scopes.CreateScope();
                var repository=scope.ServiceProvider.GetRequiredService<IPaymentExecutionRepository>();
                var mediator=scope.ServiceProvider.GetRequiredService<IMediator>();
                var now=DateTimeOffset.UtcNow;
                var ids=await repository.LeasePaymentRecoveryBatchAsync(leaseOwner,now,
                    now.AddSeconds(Math.Max(30,configured.Value.ProcessingLeaseSeconds)),
                    Math.Clamp(configured.Value.RecoveryBatchSize,1,500),stoppingToken);
                foreach(var id in ids)
                {
                    try{await mediator.Send(new RecoverGatewayPaymentCommand(id),stoppingToken);}
                    catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){throw;}
                    catch(Exception ex){logger.LogWarning(ex,"Payment recovery failed. PaymentAttemptId={PaymentAttemptId}",id);}
                }
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){ }
            catch(Exception ex){logger.LogError(ex,"Payment recovery batch failed.");}
        }
    }
}
