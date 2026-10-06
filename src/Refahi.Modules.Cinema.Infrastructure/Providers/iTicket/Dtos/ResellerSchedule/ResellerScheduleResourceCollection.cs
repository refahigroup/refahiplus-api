using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ResellerSchedule;

public sealed class ResellerScheduleResourceCollection
{
    [JsonPropertyName("data")]
    public List<ResellerScheduleResourceCollectionDataItem> Data { get; init; }

    [JsonPropertyName("links")]
    public ResellerScheduleResourceCollectionLinks Links { get; init; }

    [JsonPropertyName("meta")]
    public ResellerScheduleResourceCollectionMeta Meta { get; init; }

}
