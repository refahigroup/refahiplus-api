#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ResellerScheduleResourceCollectionDataItemAttributes
{
    [JsonPropertyName("samfa_id")]
    public int? SamfaId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; }

    [JsonPropertyName("show_id")]
    public string ShowId { get; init; }

    [JsonPropertyName("show_title")]
    public string ShowTitle { get; init; }

    [JsonPropertyName("place_id")]
    public string PlaceId { get; init; }

    [JsonPropertyName("place_title")]
    public string PlaceTitle { get; init; }

    [JsonPropertyName("hall_id")]
    public string? HallId { get; init; }

    [JsonPropertyName("hall_name")]
    public string? HallName { get; init; }

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

    [JsonPropertyName("prices")]
    public List<int> Prices { get; init; }

    [JsonPropertyName("currency_code")]
    public string? CurrencyCode { get; init; }

    [JsonPropertyName("capacity")]
    public int Capacity { get; init; }

    [JsonPropertyName("available_count")]
    public int AvailableCount { get; init; }

    [JsonPropertyName("tax_applies")]
    public bool TaxApplies { get; init; }

    [JsonPropertyName("fee_applies")]
    public bool FeeApplies { get; init; }

}
