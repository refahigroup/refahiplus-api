using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Schedule;

public sealed class SeatMapResourceAttributesSessions
{
    [JsonPropertyName("data")]
    public List<SeatMapResourceAttributesSessionsDataItem> Data { get; init; }

}
