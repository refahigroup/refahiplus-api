using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Schedule;

public sealed class ShowSchedulesResourceAttributes
{
    [JsonPropertyName("show")]
    public List<JsonElement> Show { get; init; }

    [JsonPropertyName("dates")]
    public List<JsonElement> Dates { get; init; }

}
