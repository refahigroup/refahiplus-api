using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Infrastructure.Workers;

public sealed class ProviderCallLogRetentionWorker : BackgroundService
{
    private const int RetentionDays = 180;
    private const int BatchSize = 1000;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ProviderCallLogRetentionWorker> _logger;

    public ProviderCallLogRetentionWorker(IServiceScopeFactory scopes, ILogger<ProviderCallLogRetentionWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CleanupAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await CleanupAsync(stoppingToken);
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        try
        {
            var deleted = 0;
            do
            {
                using var scope = _scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IProviderCallLogRepository>();
                deleted = await repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-RetentionDays), BatchSize, ct);
            } while (deleted == BatchSize && !ct.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provider call log retention cycle failed.");
        }
    }
}
