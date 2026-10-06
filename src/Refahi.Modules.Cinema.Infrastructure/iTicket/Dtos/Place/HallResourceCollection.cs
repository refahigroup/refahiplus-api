#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class HallResourceCollection
{
    [JsonPropertyName("data")]
    public List<HallResource> Data { get; init; }

    [JsonPropertyName("links")]
    public HallResourceCollectionLinks Links { get; init; }

    [JsonPropertyName("meta")]
    public HallResourceCollectionMeta Meta { get; init; }

}
