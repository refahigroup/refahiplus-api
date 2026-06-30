using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;
using Refahi.Modules.Hotels.Domain.Aggregates.ProviderBookingCacheAgg.Enums;

namespace Refahi.Modules.Hotels.Domain.Aggregates.ProviderBookingCacheAgg;

public sealed class HotelProviderBookingCacheEntry
{
    private HotelProviderBookingCacheEntry()
    {
    }

    public Guid Id { get; private set; }
    public string ProviderName { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public Guid SagaId { get; private set; }
    public Guid HotelRequestId { get; private set; }
    public ProviderBookingCacheStatus Status { get; private set; }
    public string? ProviderBookingCode { get; private set; }
    public string? ResponseJson { get; private set; }
    public string? FailureReason { get; private set; }
    public string? CancellationIdempotencyKey { get; private set; }
    public string? CancellationReason { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? LastAttemptAt { get; private set; }
    public DateTime? CancellationRequestedAt { get; private set; }
    public DateTime? CancellationCompletedAt { get; private set; }
    public DateTime? ExternalUnresolvedAt { get; private set; }

    public static HotelProviderBookingCacheEntry Create(
        string providerName,
        string idempotencyKey,
        string requestHash,
        Guid sagaId,
        Guid hotelRequestId,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new DomainException("ProviderName is required.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("IdempotencyKey is required.");
        if (string.IsNullOrWhiteSpace(requestHash))
            throw new DomainException("RequestHash is required.");
        if (sagaId == Guid.Empty)
            throw new DomainException("SagaId is required.");
        if (hotelRequestId == Guid.Empty)
            throw new DomainException("HotelRequestId is required.");

        return new HotelProviderBookingCacheEntry
        {
            Id = Guid.NewGuid(),
            ProviderName = providerName.Trim(),
            IdempotencyKey = idempotencyKey.Trim(),
            RequestHash = requestHash.Trim(),
            SagaId = sagaId,
            HotelRequestId = hotelRequestId,
            Status = ProviderBookingCacheStatus.InProgress,
            AttemptCount = 0,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };
    }

    public void EnsureSameRequest(string requestHash)
    {
        if (!RequestHash.Equals(requestHash, StringComparison.Ordinal))
            throw new DomainException("Provider idempotency key was reused with a different request payload.");
    }

    public void MarkAttemptStarted(DateTime nowUtc)
    {
        if (Status == ProviderBookingCacheStatus.Completed)
            return;

        Status = ProviderBookingCacheStatus.InProgress;
        AttemptCount++;
        LastAttemptAt = nowUtc;
        FailureReason = null;
        UpdatedAt = nowUtc;
    }

    public void MarkCompleted(string providerBookingCode, string responseJson, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(providerBookingCode))
            throw new DomainException("ProviderBookingCode is required.");

        ProviderBookingCode = providerBookingCode.Trim();
        ResponseJson = string.IsNullOrWhiteSpace(responseJson) ? "{}" : responseJson.Trim();
        Status = ProviderBookingCacheStatus.Completed;
        FailureReason = null;
        CompletedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    public void MarkFailed(string reason, DateTime nowUtc)
    {
        if (Status == ProviderBookingCacheStatus.Completed)
            return;

        Status = ProviderBookingCacheStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason)
            ? "Unknown provider booking failure"
            : reason.Trim()[..Math.Min(reason.Trim().Length, 1000)];
        UpdatedAt = nowUtc;
    }

    public void MarkCancellationPending(string idempotencyKey, string reason, DateTime nowUtc)
    {
        if (Status is ProviderBookingCacheStatus.Cancelled or ProviderBookingCacheStatus.ExternallyUnresolved)
            return;

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Cancellation idempotency key is required.");

        Status = ProviderBookingCacheStatus.CancellationPending;
        CancellationIdempotencyKey = idempotencyKey.Trim();
        CancellationReason = string.IsNullOrWhiteSpace(reason)
            ? "Provider booking cancellation requested."
            : reason.Trim()[..Math.Min(reason.Trim().Length, 1000)];
        CancellationRequestedAt ??= nowUtc;
        UpdatedAt = nowUtc;
    }

    public void MarkCancelled(DateTime nowUtc)
    {
        if (Status == ProviderBookingCacheStatus.Cancelled)
            return;

        Status = ProviderBookingCacheStatus.Cancelled;
        FailureReason = null;
        CancellationCompletedAt = nowUtc;
        ExternalUnresolvedAt = null;
        UpdatedAt = nowUtc;
    }

    public void MarkExternallyUnresolved(string reason, DateTime nowUtc)
    {
        if (Status == ProviderBookingCacheStatus.ExternallyUnresolved)
            return;

        Status = ProviderBookingCacheStatus.ExternallyUnresolved;
        FailureReason = string.IsNullOrWhiteSpace(reason)
            ? "Provider booking external state is unresolved."
            : reason.Trim()[..Math.Min(reason.Trim().Length, 1000)];
        ExternalUnresolvedAt = nowUtc;
        UpdatedAt = nowUtc;
    }
}
