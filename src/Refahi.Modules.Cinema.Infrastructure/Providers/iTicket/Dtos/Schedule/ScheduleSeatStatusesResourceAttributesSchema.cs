using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Schedule;

public sealed class ScheduleSeatStatusesResourceAttributesSchema
{
    [JsonPropertyName("status")]
    public List<JsonElement> Status { get; init; }

}
