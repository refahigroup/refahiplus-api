#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ShowResourceCollection
{
    [JsonPropertyName("data")]
    public List<ShowResource> Data { get; init; }

    [JsonPropertyName("links")]
    public ShowResourceCollectionLinks Links { get; init; }

    [JsonPropertyName("meta")]
    public ShowResourceCollectionMeta Meta { get; init; }

    [JsonPropertyName("category")]
    public List<JsonElement> Category { get; init; }

    [JsonPropertyName("tag")]
    public List<JsonElement> Tag { get; init; }

    [JsonPropertyName("genre")]
    public List<JsonElement> Genre { get; init; }

    [JsonPropertyName("screening")]
    public List<JsonElement> Screening { get; init; }

}
