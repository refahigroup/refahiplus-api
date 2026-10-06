#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ShowGenreResourceCollection
{
    [JsonPropertyName("data")]
    public List<ShowGenreResource> Data { get; init; }

    [JsonPropertyName("links")]
    public ShowGenreResourceCollectionLinks Links { get; init; }

    [JsonPropertyName("meta")]
    public ShowGenreResourceCollectionMeta Meta { get; init; }

}
