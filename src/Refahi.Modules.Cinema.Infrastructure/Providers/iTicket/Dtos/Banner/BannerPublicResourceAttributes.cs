using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Banner;

public sealed class BannerPublicResourceAttributes
{
    [JsonPropertyName("title")]
    public string Title { get; init; }

    [JsonPropertyName("placement_code")]
    public string PlacementCode { get; init; }

    [JsonPropertyName("image")]
    public Dictionary<string,string> Image { get; init; }

    [JsonPropertyName("link")]
    public BannerPublicResourceAttributesLink? Link { get; init; }

    [JsonPropertyName("display_weight")]
    public int DisplayWeight { get; init; }

    [JsonPropertyName("sort_order")]
    public int SortOrder { get; init; }

    [JsonPropertyName("starts_at")]
    public string? StartsAt { get; init; }

    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; init; }

}
