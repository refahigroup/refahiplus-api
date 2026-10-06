#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ScheduleSeatStatusesResourceAttributesSchema
{
    [JsonPropertyName("status")]
    public List<JsonElement> Status { get; init; }

}
