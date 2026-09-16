using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Application.Contracts.Configuration;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.Recovery;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Workers;

public sealed class FundingRecoveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FundingSafetyOptions _options;
    private readonly ILogger<FundingRecoveryWorker> _logger;

    public FundingRecoveryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<FundingSafetyOptions> options,
        ILogger<FundingRecoveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(
            Math.Max(5, _options.RecoveryPollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wallet funding recovery batch failed.");
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IPaymentGatewaySessionRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var now = DateTimeOffset.UtcNow;
        var batchSize = Math.Clamp(_options.RecoveryBatchSize, 1, 500);

        var recoveryIds = await sessions.GetRecoveryCandidatesAsync(now, batchSize, ct);
        foreach (var id in recoveryIds)
            await mediator.Send(new RecoverFundingSessionCommand(id), ct);

        var reversalIds = await sessions.GetReversalCandidatesAsync(now, batchSize, ct);
        foreach (var id in reversalIds)
            await mediator.Send(new RetryFundingReversalCommand(id), ct);
    }
}
