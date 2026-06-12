using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightOfferSnapshotAgg;

public sealed class FlightOfferSnapshot
{
    private FlightOfferSnapshot()
    {
        Id = Guid.Empty;
        OfferToken = string.Empty;
        ProviderName = string.Empty;
        ProviderFareSourceCode = string.Empty;
        Currency = string.Empty;
        PublicOfferSnapshotJson = string.Empty;
        ProviderSnapshotJson = string.Empty;
    }

    private FlightOfferSnapshot(
        Guid id,
        string offerToken,
        string providerName,
        string providerFareSourceCode,
        string? providerSearchId,
        string? providerTraceId,
        long totalFareAmount,
        string currency,
        string publicOfferSnapshotJson,
        string? providerSnapshotJson,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        Id = id;
        OfferToken = Require(offerToken, "Offer token is required.");
        ProviderName = Require(providerName, "Provider name is required.");
        ProviderFareSourceCode = Require(providerFareSourceCode, "Provider fare source code is required.");
        ProviderSearchId = string.IsNullOrWhiteSpace(providerSearchId) ? null : providerSearchId.Trim();
        ProviderTraceId = string.IsNullOrWhiteSpace(providerTraceId) ? null : providerTraceId.Trim();
        TotalFareAmount = totalFareAmount;
        Currency = Require(currency, "Currency is required.").ToUpperInvariant();
        PublicOfferSnapshotJson = Require(publicOfferSnapshotJson, "Public offer snapshot is required.");
        ProviderSnapshotJson = string.IsNullOrWhiteSpace(providerSnapshotJson) ? null : providerSnapshotJson.Trim();
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;

        if (totalFareAmount <= 0)
            throw new DomainException("Offer amount must be greater than zero.");

        if (Currency != "IRR")
            throw new DomainException("Only IRR flight offers are supported.");

        if (expiresAtUtc <= createdAtUtc)
            throw new DomainException("Offer expiration must be after creation time.");
    }

    public Guid Id { get; private set; }
    public string OfferToken { get; private set; }
    public string ProviderName { get; private set; }
    public string ProviderFareSourceCode { get; private set; }
    public string? ProviderSearchId { get; private set; }
    public string? ProviderTraceId { get; private set; }
    public long TotalFareAmount { get; private set; }
    public string Currency { get; private set; }
    public string PublicOfferSnapshotJson { get; private set; }
    public string? ProviderSnapshotJson { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    public static FlightOfferSnapshot Create(
        string offerToken,
        string providerName,
        string providerFareSourceCode,
        string? providerSearchId,
        string? providerTraceId,
        long totalFareAmount,
        string currency,
        string publicOfferSnapshotJson,
        string? providerSnapshotJson,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        return new FlightOfferSnapshot(
            Guid.NewGuid(),
            offerToken,
            providerName,
            providerFareSourceCode,
            providerSearchId,
            providerTraceId,
            totalFareAmount,
            currency,
            publicOfferSnapshotJson,
            providerSnapshotJson,
            createdAtUtc,
            expiresAtUtc);
    }

    public bool IsExpired(DateTime nowUtc) => ExpiresAtUtc <= nowUtc;

    private static string Require(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException(message);

        return value.Trim();
    }
}
