using Refahi.Modules.Flights.Domain.Abstractions;
using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;

public sealed class SelectedFareSnapshot : ValueObject
{
    private SelectedFareSnapshot()
    {
        ProviderFareId = string.Empty;
        FareCaption = string.Empty;
        CabinClass = string.Empty;
    }

    public SelectedFareSnapshot(
        string providerFareId,
        string fareCaption,
        string cabinClass,
        string? bookingClass = null,
        string? fareRulesSnapshotJson = null,
        string? providerTraceId = null)
    {
        if (string.IsNullOrWhiteSpace(providerFareId))
        {
            throw new DomainException("Provider fare id is required.");
        }

        if (string.IsNullOrWhiteSpace(fareCaption))
        {
            throw new DomainException("Fare caption is required.");
        }

        if (string.IsNullOrWhiteSpace(cabinClass))
        {
            throw new DomainException("Cabin class is required.");
        }

        ProviderFareId = providerFareId.Trim();
        FareCaption = fareCaption.Trim();
        CabinClass = cabinClass.Trim();
        BookingClass = string.IsNullOrWhiteSpace(bookingClass) ? null : bookingClass.Trim();
        FareRulesSnapshotJson = string.IsNullOrWhiteSpace(fareRulesSnapshotJson) ? null : fareRulesSnapshotJson.Trim();
        ProviderTraceId = string.IsNullOrWhiteSpace(providerTraceId) ? null : providerTraceId.Trim();
    }

    public string ProviderFareId { get; private set; }
    public string FareCaption { get; private set; }
    public string CabinClass { get; private set; }
    public string? BookingClass { get; private set; }
    public string? FareRulesSnapshotJson { get; private set; }
    public string? ProviderTraceId { get; private set; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ProviderFareId;
        yield return FareCaption;
        yield return CabinClass;
        yield return BookingClass;
        yield return FareRulesSnapshotJson;
        yield return ProviderTraceId;
    }
}
