using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;

namespace Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;

public sealed class HotelBookingSagaState
{
    private HotelBookingSagaState()
    {
    }

    public Guid SagaId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid HotelRequestId { get; private set; }
    public Guid? OrderId { get; private set; }
    public HotelBookingPaymentStatus PaymentStatus { get; private set; }
    public HotelProviderBookingStatus ProviderBookingStatus { get; private set; }
    public HotelBookingSagaStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ProviderCancellationIdempotencyKey { get; private set; }
    public string? ProviderCancellationReason { get; private set; }
    public DateTime? ProviderCancellationRequestedAt { get; private set; }
    public DateTime? ProviderCancellationCompletedAt { get; private set; }
    public DateTime? ExternalUnresolvedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public static HotelBookingSagaState Start(Guid userId, Guid hotelRequestId, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId is required.");
        if (hotelRequestId == Guid.Empty)
            throw new DomainException("HotelRequestId is required.");

        return new HotelBookingSagaState
        {
            SagaId = Guid.NewGuid(),
            UserId = userId,
            HotelRequestId = hotelRequestId,
            PaymentStatus = HotelBookingPaymentStatus.None,
            ProviderBookingStatus = HotelProviderBookingStatus.None,
            Status = HotelBookingSagaStatus.RequestCreated,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };
    }

    public void MarkOrderCreated(Guid orderId, DateTime nowUtc)
    {
        if (orderId == Guid.Empty)
            throw new DomainException("OrderId is required.");

        if (OrderId == orderId &&
            (Status == HotelBookingSagaStatus.OrderCreated ||
             Status == HotelBookingSagaStatus.PaymentPending ||
             Status == HotelBookingSagaStatus.Paid ||
             Status == HotelBookingSagaStatus.ProviderBookingStarted ||
             Status == HotelBookingSagaStatus.ProviderBookingConfirmed ||
             Status == HotelBookingSagaStatus.Completed))
        {
            return;
        }

        if (OrderId.HasValue && OrderId.Value != orderId)
            throw new DomainException("Saga is already linked to another order.");

        EnsureNotTerminal();

        OrderId = orderId;
        PaymentStatus = HotelBookingPaymentStatus.Pending;
        Status = HotelBookingSagaStatus.OrderCreated;
        FailureReason = null;
        UpdatedAt = nowUtc;
    }

    public void MarkPaymentPending(DateTime nowUtc)
    {
        if (Status == HotelBookingSagaStatus.PaymentPending)
            return;

        if (OrderId is null)
            throw new DomainException("Order must be created before payment pending.");

        EnsureNotTerminal();

        PaymentStatus = HotelBookingPaymentStatus.Pending;
        Status = HotelBookingSagaStatus.PaymentPending;
        UpdatedAt = nowUtc;
    }

    public void MarkPaid(Guid orderId, DateTime nowUtc)
    {
        if (OrderId.HasValue && OrderId.Value != orderId)
            throw new DomainException("Paid order does not match saga order.");

        if (Status is HotelBookingSagaStatus.Paid or
            HotelBookingSagaStatus.ProviderBookingStarted or
            HotelBookingSagaStatus.ProviderBookingConfirmed or
            HotelBookingSagaStatus.Completed)
        {
            return;
        }

        EnsureNotTerminal();

        OrderId = orderId;
        PaymentStatus = HotelBookingPaymentStatus.Paid;
        Status = HotelBookingSagaStatus.Paid;
        FailureReason = null;
        UpdatedAt = nowUtc;
    }

    public void MarkProviderBookingStarted(DateTime nowUtc)
    {
        if (Status == HotelBookingSagaStatus.ProviderBookingStarted)
            return;

        if (PaymentStatus != HotelBookingPaymentStatus.Paid)
            throw new DomainException("Payment must be paid before provider booking starts.");

        EnsureNotTerminal();

        ProviderBookingStatus = HotelProviderBookingStatus.Started;
        Status = HotelBookingSagaStatus.ProviderBookingStarted;
        UpdatedAt = nowUtc;
    }

    public void MarkProviderBookingConfirmed(DateTime nowUtc)
    {
        if (Status is HotelBookingSagaStatus.ProviderBookingConfirmed or HotelBookingSagaStatus.Completed)
            return;

        if (ProviderBookingStatus != HotelProviderBookingStatus.Started)
            throw new DomainException("Provider booking must be started before confirmation.");

        ProviderBookingStatus = HotelProviderBookingStatus.Confirmed;
        Status = HotelBookingSagaStatus.ProviderBookingConfirmed;
        UpdatedAt = nowUtc;
    }

    public void Complete(DateTime nowUtc)
    {
        if (Status == HotelBookingSagaStatus.Completed)
            return;

        if (ProviderBookingStatus != HotelProviderBookingStatus.Confirmed)
            throw new DomainException("Provider booking must be confirmed before completion.");

        Status = HotelBookingSagaStatus.Completed;
        CompletedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    public void Fail(string reason, DateTime nowUtc)
    {
        if (Status == HotelBookingSagaStatus.Failed)
            return;

        Status = HotelBookingSagaStatus.Failed;
        if (PaymentStatus != HotelBookingPaymentStatus.Paid)
            PaymentStatus = HotelBookingPaymentStatus.Failed;
        FailureReason = NormalizeReason(reason);
        UpdatedAt = nowUtc;
    }

    public void RecordRecoverableFailure(string reason, DateTime nowUtc)
    {
        if (Status is HotelBookingSagaStatus.Completed or HotelBookingSagaStatus.Compensated)
            return;

        FailureReason = NormalizeReason(reason);
        UpdatedAt = nowUtc;
    }

    public void Compensate(string reason, DateTime nowUtc)
    {
        if (Status == HotelBookingSagaStatus.Compensated)
            return;

        Status = HotelBookingSagaStatus.Compensated;
        PaymentStatus = HotelBookingPaymentStatus.Refunded;
        ProviderBookingStatus = HotelProviderBookingStatus.Failed;
        FailureReason = NormalizeReason(reason);
        UpdatedAt = nowUtc;
    }

    public void MarkProviderCancellationPending(
        string idempotencyKey,
        string reason,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Provider cancellation idempotency key is required.");

        if (ProviderBookingStatus is HotelProviderBookingStatus.Cancelled or
            HotelProviderBookingStatus.ExternallyUnresolved)
        {
            return;
        }

        ProviderBookingStatus = HotelProviderBookingStatus.CancellationPending;
        ProviderCancellationIdempotencyKey = idempotencyKey.Trim();
        ProviderCancellationReason = NormalizeReason(reason);
        ProviderCancellationRequestedAt ??= nowUtc;
        UpdatedAt = nowUtc;
    }

    public void MarkProviderCancelled(DateTime nowUtc)
    {
        if (ProviderBookingStatus == HotelProviderBookingStatus.Cancelled)
            return;

        ProviderBookingStatus = HotelProviderBookingStatus.Cancelled;
        ProviderCancellationCompletedAt = nowUtc;
        ExternalUnresolvedAt = null;
        UpdatedAt = nowUtc;
    }

    public void MarkExternalUnresolved(string reason, DateTime nowUtc)
    {
        if (ProviderBookingStatus == HotelProviderBookingStatus.ExternallyUnresolved)
            return;

        ProviderBookingStatus = HotelProviderBookingStatus.ExternallyUnresolved;
        FailureReason = NormalizeReason(reason);
        ExternalUnresolvedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    private void EnsureNotTerminal()
    {
        if (Status is HotelBookingSagaStatus.Failed or
            HotelBookingSagaStatus.Compensated or
            HotelBookingSagaStatus.Completed)
        {
            throw new DomainException($"Cannot transition saga from {Status} state.");
        }
    }

    private static string NormalizeReason(string reason)
        => string.IsNullOrWhiteSpace(reason)
            ? "Unknown failure"
            : reason.Trim()[..Math.Min(reason.Trim().Length, 1000)];
}
