#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class SeatMapResourceAttributes
{
    [JsonPropertyName("show")]
    public JsonElement Show { get; init; }

    [JsonPropertyName("sessions")]
    public SeatMapResourceAttributesSessions Sessions { get; init; }

    [JsonPropertyName("place")]
    public JsonElement Place { get; init; }

    [JsonPropertyName("hall_id")]
    public string? HallId { get; init; }

    [JsonPropertyName("currency_code")]
    public string CurrencyCode { get; init; }

    [JsonPropertyName("currency_code_label")]
    public string? CurrencyCodeLabel { get; init; }

    [JsonPropertyName("schema")]
    public SeatMapResourceAttributesSchema Schema { get; init; }

    [JsonPropertyName("seat_types")]
    public List<List<string?>> SeatTypes { get; init; }

    [JsonPropertyName("blocks")]
    public List<SeatMapResourceAttributesBlock> Blocks { get; init; }

}
