#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class HallZoneResourceCollectionLinks
{
    [JsonPropertyName("first")]
    public string First { get; init; }

    [JsonPropertyName("last")]
    public string Last { get; init; }

    [JsonPropertyName("prev")]
    public string? Prev { get; init; }

    [JsonPropertyName("next")]
    public string? Next { get; init; }

}
