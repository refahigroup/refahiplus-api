using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CancelProviderBooking;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;

namespace Refahi.Modules.Hotels.Application.HotelRequests.CancelProviderBooking;

public sealed class CancelProviderBookingCommandHandler
    : IRequestHandler<CancelProviderBookingCommand, CancelProviderBookingResponse>
{
    private readonly IHotelRequestRepository _requestRepository;
    private readonly IHotelBookingSagaRepository _sagaRepository;
    private readonly IHotelProviderBookingCacheRepository _providerBookingCacheRepository;
    private readonly IHotelProvider _provider;
    private readonly ILogger<CancelProviderBookingCommandHandler> _logger;

    public CancelProviderBookingCommandHandler(
        IHotelRequestRepository requestRepository,
        IHotelBookingSagaRepository sagaRepository,
        IHotelProviderBookingCacheRepository providerBookingCacheRepository,
        IHotelProvider provider,
        ILogger<CancelProviderBookingCommandHandler> logger)
    {
        _requestRepository = requestRepository;
        _sagaRepository = sagaRepository;
        _providerBookingCacheRepository = providerBookingCacheRepository;
        _provider = provider;
        _logger = logger;
    }

    public async Task<CancelProviderBookingResponse> Handle(
        CancelProviderBookingCommand request,
        CancellationToken cancellationToken)
    {
        var saga = await _sagaRepository.GetAsync(request.SagaId, cancellationToken)
            ?? throw new InvalidOperationException("Hotel booking saga was not found.");

        var hotelRequest = await _requestRepository.GetAsync(saga.HotelRequestId, cancellationToken)
            ?? throw new InvalidOperationException("Hotel request was not found.");

        var cacheEntry = await _providerBookingCacheRepository.GetBySagaIdAsync(saga.SagaId, cancellationToken);
        var providerBookingCode = hotelRequest.ProviderBookingCode ?? cacheEntry?.ProviderBookingCode;

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = saga.UserId,
            ["SagaId"] = saga.SagaId,
            ["HotelRequestId"] = saga.HotelRequestId,
            ["OrderId"] = saga.OrderId,
            ["ProviderBookingCode"] = providerBookingCode
        });

        if (string.IsNullOrWhiteSpace(providerBookingCode))
        {
            _logger.LogInformation(
                "Skipping provider cancellation because no provider booking code exists. SagaId={SagaId}",
                saga.SagaId);

            return new CancelProviderBookingResponse(
                saga.SagaId,
                saga.HotelRequestId,
                null,
                "NoProviderBooking",
                false,
                false);
        }

        if (saga.ProviderBookingStatus == HotelProviderBookingStatus.Cancelled)
        {
            return new CancelProviderBookingResponse(
                saga.SagaId,
                saga.HotelRequestId,
                providerBookingCode,
                "Cancelled",
                false,
                false);
        }

        if (saga.ProviderBookingStatus == HotelProviderBookingStatus.ExternallyUnresolved)
        {
            return new CancelProviderBookingResponse(
                saga.SagaId,
                saga.HotelRequestId,
                providerBookingCode,
                "ExternallyUnresolved",
                false,
                true);
        }

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"hotel-provider-cancel-{saga.SagaId:N}"
            : request.IdempotencyKey.Trim();
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? "Provider booking cancellation requested."
            : request.Reason.Trim();
        var now = DateTime.UtcNow;

        saga.MarkProviderCancellationPending(idempotencyKey, reason, now);
        cacheEntry?.MarkCancellationPending(idempotencyKey, reason, now);
        await _sagaRepository.SaveChangesAsync(cancellationToken);
        await _providerBookingCacheRepository.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Attempting hotel provider cancellation. SagaId={SagaId}, Provider={Provider}, ProviderBookingCode={ProviderBookingCode}, CancellationIdempotencyKey={CancellationIdempotencyKey}",
            saga.SagaId,
            hotelRequest.ProviderName,
            providerBookingCode,
            idempotencyKey);

        var providerResult = await _provider.CancelBookingAsync(providerBookingCode, idempotencyKey, reason);
        now = DateTime.UtcNow;

        if (providerResult.IsCancelled)
        {
            saga.MarkProviderCancelled(now);
            cacheEntry?.MarkCancelled(now);
            await _sagaRepository.SaveChangesAsync(cancellationToken);
            await _providerBookingCacheRepository.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Hotel provider booking cancellation completed. SagaId={SagaId}, ProviderBookingCode={ProviderBookingCode}, Status={Status}",
                saga.SagaId,
                providerBookingCode,
                providerResult.Status);

            return new CancelProviderBookingResponse(
                saga.SagaId,
                saga.HotelRequestId,
                providerBookingCode,
                providerResult.Status,
                true,
                false);
        }

        var unresolvedReason = providerResult.IsUnsupported
            ? $"Provider cancellation unsupported: {providerResult.ProviderMessage}"
            : $"Provider cancellation failed: {providerResult.ProviderMessage ?? providerResult.Status}";

        saga.MarkExternalUnresolved(unresolvedReason, now);
        cacheEntry?.MarkExternallyUnresolved(unresolvedReason, now);
        await _sagaRepository.SaveChangesAsync(cancellationToken);
        await _providerBookingCacheRepository.SaveChangesAsync(cancellationToken);

        _logger.LogError(
            "Hotel provider booking remains externally unresolved. SagaId={SagaId}, ProviderBookingCode={ProviderBookingCode}, Outcome={Outcome}",
            saga.SagaId,
            providerBookingCode,
            providerResult.Status);

        return new CancelProviderBookingResponse(
            saga.SagaId,
            saga.HotelRequestId,
            providerBookingCode,
            "ExternallyUnresolved",
            true,
            true);
    }
}
