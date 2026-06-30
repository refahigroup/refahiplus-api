using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CancelProviderBooking;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.FinalizeHotelBookingAfterPayment;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;

namespace Refahi.Modules.Hotels.Infrastructure.Workers;

public sealed class HotelProviderReconciliationWorker : BackgroundService
{
    private static readonly HotelBookingSagaStatus[] CandidateStatuses =
    [
        HotelBookingSagaStatus.Paid,
        HotelBookingSagaStatus.ProviderBookingStarted
    ];

    private static readonly HotelBookingSagaStatus[] ExternalCancellationCandidateStatuses =
    [
        HotelBookingSagaStatus.Compensated,
        HotelBookingSagaStatus.Failed
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HotelProviderReconciliationWorker> _logger;

    public HotelProviderReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<HotelProviderReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hotel provider reconciliation cycle failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ReconcileOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sagaRepository = scope.ServiceProvider.GetRequiredService<IHotelBookingSagaRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var candidates = await sagaRepository.GetStuckAsync(
            CandidateStatuses,
            DateTime.UtcNow.AddMinutes(-5),
            25,
            cancellationToken);

        foreach (var saga in candidates)
        {
            if (saga.OrderId is null)
                continue;

            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["UserId"] = saga.UserId,
                ["SagaId"] = saga.SagaId,
                ["HotelRequestId"] = saga.HotelRequestId,
                ["OrderId"] = saga.OrderId,
                ["ProviderBookingCode"] = null
            });

            try
            {
                _logger.LogWarning(
                    "Reconciling stuck hotel provider booking saga. SagaId={SagaId}, HotelRequestId={HotelRequestId}, OrderId={OrderId}, Status={Status}",
                    saga.SagaId,
                    saga.HotelRequestId,
                    saga.OrderId,
                    saga.Status);

                await mediator.Send(new FinalizeHotelBookingAfterPaymentCommand(
                    saga.OrderId.Value,
                    saga.UserId,
                    Guid.Empty,
                    saga.SagaId), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Hotel provider reconciliation failed for saga. SagaId={SagaId}, HotelRequestId={HotelRequestId}, OrderId={OrderId}",
                    saga.SagaId,
                    saga.HotelRequestId,
                    saga.OrderId);
            }
        }

        var cancellationCandidates = await sagaRepository.GetStuckAsync(
            ExternalCancellationCandidateStatuses,
            DateTime.UtcNow.AddMinutes(-5),
            25,
            cancellationToken);

        foreach (var saga in cancellationCandidates)
        {
            if (saga.ProviderBookingStatus is HotelProviderBookingStatus.Cancelled or
                HotelProviderBookingStatus.ExternallyUnresolved or
                HotelProviderBookingStatus.None)
            {
                continue;
            }

            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["UserId"] = saga.UserId,
                ["SagaId"] = saga.SagaId,
                ["HotelRequestId"] = saga.HotelRequestId,
                ["OrderId"] = saga.OrderId,
                ["ProviderBookingCode"] = null
            });

            try
            {
                _logger.LogWarning(
                    "Reconciling externally inconsistent hotel provider booking. SagaId={SagaId}, HotelRequestId={HotelRequestId}, OrderId={OrderId}, Status={Status}, ProviderStatus={ProviderStatus}",
                    saga.SagaId,
                    saga.HotelRequestId,
                    saga.OrderId,
                    saga.Status,
                    saga.ProviderBookingStatus);

                await mediator.Send(new CancelProviderBookingCommand(
                    saga.SagaId,
                    $"Saga is {saga.Status}; external provider booking must be cancelled or marked unresolved."),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Hotel provider external-state reconciliation failed for saga. SagaId={SagaId}, HotelRequestId={HotelRequestId}, OrderId={OrderId}",
                    saga.SagaId,
                    saga.HotelRequestId,
                    saga.OrderId);
            }
        }
    }
}
