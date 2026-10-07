using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Cinema.Domain;
using Refahi.Modules.Cinema.Application.Contracts;
namespace Refahi.Modules.Cinema.Infrastructure.Workers;
public sealed class CinemaReconciliationWorker(IServiceScopeFactory scopes,IOptions<CinemaOptions> options,ILogger<CinemaReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();
                var repository=scope.ServiceProvider.GetRequiredService<ICinemaOrderRepository>();
                for(var skip=0;!stoppingToken.IsCancellationRequested;skip+=50)
                {
                    var ids=await repository.CandidatesAsync(stoppingToken,skip);
                    if(ids.Count==0)break;
                    foreach(var id in ids)
                    {
                    using var item=scopes.CreateScope();
                    try {await item.ServiceProvider.GetRequiredService<IMediator>().Send(new ReconcileCinemaOrderCommand(id),stoppingToken);}
                    catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
                    catch(Exception ex){logger.LogWarning("Cinema reconciliation pending. CinemaOrderId={CinemaOrderId} ErrorType={ErrorType}",id,ex.GetType().Name);}
                    }
                }
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
            catch(Exception ex){logger.LogError("Cinema worker cycle failed. ErrorType={ErrorType}",ex.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5,options.Value.WorkerSeconds)),stoppingToken);
        }
    }
}
