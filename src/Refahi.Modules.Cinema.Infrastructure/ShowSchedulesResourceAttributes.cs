#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ShowSchedulesResourceAttributes
{
    [JsonPropertyName("show")]
    public List<JsonElement> Show { get; init; }

    [JsonPropertyName("dates")]
    public List<JsonElement> Dates { get; init; }

}
