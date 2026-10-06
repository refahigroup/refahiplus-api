#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class SeatMapResourceAttributesSchema
{
    [JsonPropertyName("seat")]
    public List<JsonElement> Seat { get; init; }

    [JsonPropertyName("status")]
    public List<JsonElement> Status { get; init; }

    [JsonPropertyName("seat_type")]
    public List<JsonElement> SeatType { get; init; }

}
