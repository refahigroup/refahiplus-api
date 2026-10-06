#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ReserveResellerOrderRequestDataAttributes
{
    [JsonPropertyName("schedule_id")]
    public string ScheduleId { get; init; }

    [JsonPropertyName("seat_ids")]
    public List<string> SeatIds { get; init; }

    [JsonPropertyName("customer")]
    public ReserveResellerOrderRequestDataAttributesCustomer Customer { get; init; }

}
