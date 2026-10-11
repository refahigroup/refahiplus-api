using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
namespace Refahi.Modules.Hotels.Infrastructure.Workers;

public sealed class HotelCatalogueRecoveryWorker(IServiceScopeFactory scopes, ILogger<HotelCatalogueRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RecoverHotelCatalogueCommand(), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError("Hotel catalogue recovery failed ErrorType={ErrorType}", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
