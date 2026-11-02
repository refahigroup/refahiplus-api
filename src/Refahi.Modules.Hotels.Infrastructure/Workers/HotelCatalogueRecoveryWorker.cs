using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
namespace Refahi.Modules.Hotels.Infrastructure.Workers;

public sealed class HotelCatalogueRecoveryWorker(IServiceScopeFactory scopes, ILogger<HotelCatalogueRecoveryWorker> logger, IOptions<HotelLifecycleOptions> options) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(RunAsync(false, stoppingToken), RunAsync(true, stoppingToken));
    private async Task RunAsync(bool reconciliationOnly, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RecoverHotelCatalogueCommand(reconciliationOnly), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError("Hotel catalogue recovery failed ErrorType={ErrorType}", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.RecoveryIntervalSeconds), stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
