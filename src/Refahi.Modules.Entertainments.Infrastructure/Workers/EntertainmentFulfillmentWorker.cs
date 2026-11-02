using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts;

namespace Refahi.Modules.Entertainments.Infrastructure.Workers;

public sealed class EntertainmentFulfillmentWorker(IServiceScopeFactory scopes, ILogger<EntertainmentFulfillmentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (!stoppingToken.IsCancellationRequested)
        {
            try 
            { 
                using var scope = scopes.CreateScope(); 
                
                await scope.ServiceProvider
                           .GetRequiredService<IMediator>()
                           .Send(new ProcessEntertainmentFulfillmentBatchCommand(), stoppingToken); 

            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) 
            { 
                break; 
            }
            catch (Exception ex) 
            { 
                logger.LogError(ex, "Entertainment fulfillment worker iteration failed");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken)) 
                break;
        }
    }
}
