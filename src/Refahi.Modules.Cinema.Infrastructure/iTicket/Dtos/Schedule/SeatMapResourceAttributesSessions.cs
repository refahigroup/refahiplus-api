#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class SeatMapResourceAttributesSessions
{
    [JsonPropertyName("data")]
    public List<SeatMapResourceAttributesSessionsDataItem> Data { get; init; }

}
