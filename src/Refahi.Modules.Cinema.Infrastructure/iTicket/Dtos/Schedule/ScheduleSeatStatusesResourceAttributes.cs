#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ScheduleSeatStatusesResourceAttributes
{
    [JsonPropertyName("schema")]
    public ScheduleSeatStatusesResourceAttributesSchema Schema { get; init; }

    [JsonPropertyName("seats")]
    public JsonElement Seats { get; init; }

}
