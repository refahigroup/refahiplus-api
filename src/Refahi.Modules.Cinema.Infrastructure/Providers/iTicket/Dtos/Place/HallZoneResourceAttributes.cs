using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Place;

public sealed class HallZoneResourceAttributes
{
    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("color")]
    public string? Color { get; init; }

    [JsonPropertyName("sort_order")]
    public int SortOrder { get; init; }

    [JsonPropertyName("grid_row_count")]
    public int? GridRowCount { get; init; }

    [JsonPropertyName("grid_column_count")]
    public int? GridColumnCount { get; init; }

    [JsonPropertyName("row_labels")]
    public List<HallZoneResourceAttributesRowLabelsItem> RowLabels { get; init; }

    [JsonPropertyName("default_base_price")]
    public int? DefaultBasePrice { get; init; }

    [JsonPropertyName("default_currency_code")]
    public string? DefaultCurrencyCode { get; init; }

    [JsonPropertyName("default_currency_label")]
    public string DefaultCurrencyLabel { get; init; }

    [JsonPropertyName("default_seat_type")]
    public HallZoneResourceAttributesDefaultSeatType? DefaultSeatType { get; init; }

    [JsonPropertyName("seats")]
    public List<JsonElement> Seats { get; init; }

}
