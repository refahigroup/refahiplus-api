#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class HallZoneResourceCollection
{
    [JsonPropertyName("data")]
    public List<HallZoneResource> Data { get; init; }

    [JsonPropertyName("links")]
    public HallZoneResourceCollectionLinks Links { get; init; }

    [JsonPropertyName("meta")]
    public HallZoneResourceCollectionMeta Meta { get; init; }

}
