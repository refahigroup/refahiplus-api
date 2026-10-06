#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket.Dtos.Banner;

public sealed class BannerPlacementResourceCollectionDataItemAttributes
{
    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("code")]
    public string Code { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("sort_order")]
    public int SortOrder { get; init; }

}
