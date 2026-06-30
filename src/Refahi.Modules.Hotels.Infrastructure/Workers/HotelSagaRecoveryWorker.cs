using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.ConvertHotelRequestToOrder;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.FinalizeHotelBookingAfterPayment;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;

namespace Refahi.Modules.Hotels.Infrastructure.Workers;

public sealed class HotelSagaRecoveryWorker : BackgroundService
{
    private static readonly HotelBookingSagaStatus[] CandidateStatuses =
    [
        HotelBookingSagaStatus.RequestCreated,
        HotelBookingSagaStatus.OrderCreated,
        HotelBookingSagaStatus.PaymentPending,
        HotelBookingSagaStatus.Paid,
        HotelBookingSagaStatus.ProviderBookingStarted
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HotelSagaRecoveryWorker> _logger;

    public HotelSagaRecoveryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<HotelSagaRecoveryWorker> logger)
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
                await RecoverOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hotel saga recovery cycle failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }

    private async Task RecoverOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sagaRepository = scope.ServiceProvider.GetRequiredService<IHotelBookingSagaRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var candidates = await sagaRepository.GetStuckAsync(
            CandidateStatuses,
            DateTime.UtcNow.AddMinutes(-3),
            25,
            cancellationToken);

        foreach (var saga in candidates)
        {
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
                if (saga.OrderId is null)
                {
                    _logger.LogWarning(
                        "Recovering hotel saga without order. SagaId={SagaId}, RequestId={RequestId}",
                        saga.SagaId,
                        saga.HotelRequestId);

                    await mediator.Send(new ConvertHotelRequestToOrderCommand(
                        saga.HotelRequestId,
                        saga.UserId,
                        $"hotel-saga-recovery-{saga.SagaId:N}"), cancellationToken);

                    continue;
                }

                if (saga.Status is HotelBookingSagaStatus.Paid or HotelBookingSagaStatus.ProviderBookingStarted)
                {
                    _logger.LogWarning(
                        "Recovering paid hotel saga. SagaId={SagaId}, OrderId={OrderId}, Status={Status}",
                        saga.SagaId,
                        saga.OrderId,
                        saga.Status);

                    await mediator.Send(new FinalizeHotelBookingAfterPaymentCommand(
                        saga.OrderId.Value,
                        saga.UserId,
                        Guid.Empty,
                        saga.SagaId), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Hotel saga recovery failed for saga. SagaId={SagaId}, HotelRequestId={HotelRequestId}, OrderId={OrderId}",
                    saga.SagaId,
                    saga.HotelRequestId,
                    saga.OrderId);
            }
        }
    }
}
