#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket.Dtos.Banner;

public sealed class BannerPlacementResourceCollection
{
    [JsonPropertyName("data")]
    public List<BannerPlacementResourceCollectionDataItem> Data { get; init; }

}
