using Refahi.Modules.Flights.Domain.Abstractions;
using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;

public sealed class ProviderBookingSnapshot : ValueObject
{
    private ProviderBookingSnapshot()
    {
        ProviderBookingId = string.Empty;
        ProviderBookingCaption = string.Empty;
    }

    public ProviderBookingSnapshot(
        string providerBookingId,
        string providerBookingCaption,
        DateTime bookedAtUtc,
        string? providerPnr = null,
        string? providerTraceId = null,
        string? snapshotJson = null)
    {
        if (string.IsNullOrWhiteSpace(providerBookingId))
        {
            throw new DomainException("Provider booking id is required.");
        }

        if (string.IsNullOrWhiteSpace(providerBookingCaption))
        {
            throw new DomainException("Provider booking caption is required.");
        }

        ProviderBookingId = providerBookingId.Trim();
        ProviderBookingCaption = providerBookingCaption.Trim();
        BookedAtUtc = bookedAtUtc;
        ProviderPnr = string.IsNullOrWhiteSpace(providerPnr) ? null : providerPnr.Trim();
        ProviderTraceId = string.IsNullOrWhiteSpace(providerTraceId) ? null : providerTraceId.Trim();
        SnapshotJson = string.IsNullOrWhiteSpace(snapshotJson) ? null : snapshotJson.Trim();
    }

    public string ProviderBookingId { get; private set; }
    public string ProviderBookingCaption { get; private set; }
    public string? ProviderPnr { get; private set; }
    public string? ProviderTraceId { get; private set; }
    public string? SnapshotJson { get; private set; }
    public DateTime BookedAtUtc { get; private set; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ProviderBookingId;
        yield return ProviderBookingCaption;
        yield return ProviderPnr;
        yield return ProviderTraceId;
        yield return SnapshotJson;
        yield return BookedAtUtc;
    }
}
