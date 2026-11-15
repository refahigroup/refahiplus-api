using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Refahi.Modules.Cinema.Application.Contracts;

namespace Refahi.Modules.Cinema.Infrastructure.Workers;

internal interface ICinemaDiscoverySync
{
    Task RunAsync(CancellationToken ct);
}

internal sealed class CinemaDiscoveryWorker(IServiceScopeFactory scopes, IOptions<CinemaDiscoveryOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.RefreshMinutes));
        do
        {
            await using var scope = scopes.CreateAsyncScope();
            foreach (var sync in scope.ServiceProvider.GetServices<ICinemaDiscoverySync>())
                await sync.RunAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
