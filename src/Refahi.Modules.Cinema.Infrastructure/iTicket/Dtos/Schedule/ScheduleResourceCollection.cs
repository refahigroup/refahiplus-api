#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ScheduleResourceCollection
{
    [JsonPropertyName("data")]
    public List<ScheduleResourceCollectionDataItem> Data { get; init; }

}
