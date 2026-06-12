using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Enums;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;
using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Entities;

public sealed class CancellationRequest
{
    private CancellationRequest()
    {
        Reason = string.Empty;
        Quote = new CancellationQuoteSnapshot(Money.Zero(), Money.Zero(), DateTime.MinValue);
    }

    public CancellationRequest(
        Guid id,
        CancellationQuoteSnapshot quote,
        string reason,
        DateTime requestedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Cancellation request id cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Cancellation reason is required.");
        }

        Id = id;
        Quote = quote;
        Reason = reason.Trim();
        RequestedAtUtc = requestedAtUtc;
        Status = CancellationRequestStatus.Requested;
    }

    public Guid Id { get; private set; }
    public CancellationQuoteSnapshot Quote { get; private set; }
    public string Reason { get; private set; }
    public CancellationRequestStatus Status { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ProviderCancellationId { get; private set; }

    public void MarkCancelled(string? providerCancellationId, DateTime completedAtUtc)
    {
        if (Status != CancellationRequestStatus.Requested)
        {
            throw new DomainException("Only requested cancellation can be completed.");
        }

        Status = CancellationRequestStatus.Cancelled;
        CompletedAtUtc = completedAtUtc;
        ProviderCancellationId = string.IsNullOrWhiteSpace(providerCancellationId) ? null : providerCancellationId.Trim();
    }

    public void MarkFailed(string reason, DateTime completedAtUtc)
    {
        if (Status != CancellationRequestStatus.Requested)
        {
            throw new DomainException("Only requested cancellation can fail.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Cancellation failure reason is required.");
        }

        Status = CancellationRequestStatus.Failed;
        FailureReason = reason.Trim();
        CompletedAtUtc = completedAtUtc;
    }
}
