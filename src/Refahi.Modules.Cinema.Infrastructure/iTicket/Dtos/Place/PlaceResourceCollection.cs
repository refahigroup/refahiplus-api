#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class PlaceResourceCollection
{
    [JsonPropertyName("data")]
    public List<PlaceResource> Data { get; init; }

    [JsonPropertyName("links")]
    public PlaceResourceCollectionLinks Links { get; init; }

    [JsonPropertyName("meta")]
    public PlaceResourceCollectionMeta Meta { get; init; }

}
