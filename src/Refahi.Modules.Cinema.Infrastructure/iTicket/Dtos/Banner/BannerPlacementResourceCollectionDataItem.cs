#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket.Dtos.Banner;

public sealed class BannerPlacementResourceCollectionDataItem
{
    [JsonPropertyName("type")]
    public string Type { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("attributes")]
    public BannerPlacementResourceCollectionDataItemAttributes Attributes { get; init; }

}
