using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Session;

public sealed class SessionResourceCollectionDataItemAttributes
{
    [JsonPropertyName("status")]
    public string Status { get; init; }

    [JsonPropertyName("status_label")]
    public string StatusLabel { get; init; }

    [JsonPropertyName("capacity")]
    public int Capacity { get; init; }

    [JsonPropertyName("available_count")]
    public int AvailableCount { get; init; }

    [JsonPropertyName("starts_at")]
    public string StartsAt { get; init; }

    [JsonPropertyName("purchase_start_at")]
    public string? PurchaseStartAt { get; init; }

    [JsonPropertyName("purchase_end_at")]
    public string? PurchaseEndAt { get; init; }

    [JsonPropertyName("price")]
    public int? Price { get; init; }

    [JsonPropertyName("is_multi_price")]
    public bool IsMultiPrice { get; init; }

    [JsonPropertyName("price_label")]
    public string? PriceLabel { get; init; }

    [JsonPropertyName("session_label")]
    public string? SessionLabel { get; init; }

    [JsonPropertyName("prices")]
    public List<int> Prices { get; init; }

    [JsonPropertyName("currency_code")]
    public string? CurrencyCode { get; init; }

    [JsonPropertyName("currency_code_label")]
    public string? CurrencyCodeLabel { get; init; }

    [JsonPropertyName("tax_applies")]
    public bool TaxApplies { get; init; }

    [JsonPropertyName("fee_applies")]
    public bool FeeApplies { get; init; }

    [JsonPropertyName("hall_id")]
    public string? HallId { get; init; }

    [JsonPropertyName("hall_name")]
    public string? HallName { get; init; }

}
