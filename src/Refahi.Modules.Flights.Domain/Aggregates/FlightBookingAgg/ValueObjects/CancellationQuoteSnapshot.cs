using Refahi.Modules.Flights.Domain.Abstractions;
using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;

public sealed class CancellationQuoteSnapshot : ValueObject
{
    private CancellationQuoteSnapshot()
    {
        PenaltyAmount = Money.Zero();
        RefundAmount = Money.Zero();
    }

    public CancellationQuoteSnapshot(
        Money penaltyAmount,
        Money refundAmount,
        DateTime quotedAtUtc,
        DateTime? expiresAtUtc = null,
        string? providerCancellationQuoteId = null,
        string? providerTraceId = null,
        string? snapshotJson = null)
    {
        penaltyAmount.EnsureSameCurrency(refundAmount);

        if (expiresAtUtc.HasValue && expiresAtUtc.Value <= quotedAtUtc)
        {
            throw new DomainException("Cancellation quote expiration must be after quote time.");
        }

        PenaltyAmount = penaltyAmount;
        RefundAmount = refundAmount;
        QuotedAtUtc = quotedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        ProviderCancellationQuoteId = string.IsNullOrWhiteSpace(providerCancellationQuoteId) ? null : providerCancellationQuoteId.Trim();
        ProviderTraceId = string.IsNullOrWhiteSpace(providerTraceId) ? null : providerTraceId.Trim();
        SnapshotJson = string.IsNullOrWhiteSpace(snapshotJson) ? null : snapshotJson.Trim();
    }

    public Money PenaltyAmount { get; private set; }
    public Money RefundAmount { get; private set; }
    public DateTime QuotedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public string? ProviderCancellationQuoteId { get; private set; }
    public string? ProviderTraceId { get; private set; }
    public string? SnapshotJson { get; private set; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PenaltyAmount;
        yield return RefundAmount;
        yield return QuotedAtUtc;
        yield return ExpiresAtUtc;
        yield return ProviderCancellationQuoteId;
        yield return ProviderTraceId;
        yield return SnapshotJson;
    }
}
