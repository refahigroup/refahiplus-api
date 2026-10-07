using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ShowArtist;

public sealed class ShowArtistResourceCollection
{
    [JsonPropertyName("data")]
    public List<ShowArtistResource> Data { get; init; }

    [JsonPropertyName("links")]
    public ShowArtistResourceCollectionLinks Links { get; init; }

    [JsonPropertyName("meta")]
    public ShowArtistResourceCollectionMeta Meta { get; init; }

}
