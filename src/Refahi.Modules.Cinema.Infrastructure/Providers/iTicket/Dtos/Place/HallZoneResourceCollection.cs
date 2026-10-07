using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Place;

public sealed class HallZoneResourceCollection
{
    [JsonPropertyName("data")]
    public List<HallZoneResource> Data { get; init; }

    [JsonPropertyName("links")]
    public HallZoneResourceCollectionLinks Links { get; init; }

    [JsonPropertyName("meta")]
    public HallZoneResourceCollectionMeta Meta { get; init; }

}
