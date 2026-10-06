using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Banner;

public sealed class BannerPublicResourceAttributesLink
{
    [JsonPropertyName("type")]
    public string Type { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}