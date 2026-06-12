using Refahi.Modules.Flights.Domain.Abstractions;
using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;

public sealed class ProviderSnapshot : ValueObject
{
    private ProviderSnapshot()
    {
        ProviderName = string.Empty;
        ProviderId = string.Empty;
        ProviderCaption = string.Empty;
    }

    public ProviderSnapshot(
        string providerName,
        string providerId,
        string providerCaption,
        string? providerTraceId = null,
        string? snapshotJson = null)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new DomainException("Provider name is required.");
        }

        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new DomainException("Provider id is required.");
        }

        if (string.IsNullOrWhiteSpace(providerCaption))
        {
            throw new DomainException("Provider caption is required.");
        }

        ProviderName = providerName.Trim();
        ProviderId = providerId.Trim();
        ProviderCaption = providerCaption.Trim();
        ProviderTraceId = string.IsNullOrWhiteSpace(providerTraceId) ? null : providerTraceId.Trim();
        SnapshotJson = string.IsNullOrWhiteSpace(snapshotJson) ? null : snapshotJson.Trim();
    }

    public string ProviderName { get; private set; }
    public string ProviderId { get; private set; }
    public string ProviderCaption { get; private set; }
    public string? ProviderTraceId { get; private set; }
    public string? SnapshotJson { get; private set; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ProviderName;
        yield return ProviderId;
        yield return ProviderCaption;
        yield return ProviderTraceId;
        yield return SnapshotJson;
    }
}
