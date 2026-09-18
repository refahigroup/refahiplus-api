using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public sealed class ExpirationWorker(
    IServiceScopeFactory scopes,
    IOptions<PaymentTerminationOptions> configured,
    TimeProvider clock,
    ILogger<ExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(
            Math.Max(5,configured.Value.ExpirationPollSeconds)),clock);
        while(await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope=scopes.CreateScope();
                var repository=scope.ServiceProvider.GetRequiredService<IPaymentTerminationRepository>();
                var mediator=scope.ServiceProvider.GetRequiredService<IMediator>();
                var claims=await repository.ClaimDueExpirationsAsync(clock.GetUtcNow(),
                    Math.Clamp(configured.Value.ExpirationBatchSize,1,500),stoppingToken);
                foreach(var claim in claims.Where(x=>x.RecoveryAttemptId.HasValue))
                    await mediator.Send(new ResolvePaymentTerminationCommand(claim.GatewayPaymentId),stoppingToken);
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){ }
            catch(Exception ex){logger.LogError(ex,"Payment expiration batch failed.");}
        }
    }
}
