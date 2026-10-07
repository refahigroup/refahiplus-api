using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Banner;

public sealed class BannerResourceCollection
{
    [JsonPropertyName("data")]
    public List<BannerPublicResource> Data { get; init; }

    [JsonPropertyName("links")]
    public BannerResourceCollectionLinks Links { get; init; }

    [JsonPropertyName("meta")]
    public BannerResourceCollectionMeta Meta { get; init; }

}
