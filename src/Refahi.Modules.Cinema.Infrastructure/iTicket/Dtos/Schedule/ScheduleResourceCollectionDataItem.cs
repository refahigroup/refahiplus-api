#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ScheduleResourceCollectionDataItem
{
    [JsonPropertyName("type")]
    public string Type { get; init; }

    [JsonPropertyName("date")]
    public string Date { get; init; }

    [JsonPropertyName("weekday")]
    public string Weekday { get; init; }

    [JsonPropertyName("jalali_day_month")]
    public string JalaliDayMonth { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; }

    [JsonPropertyName("status_label")]
    public string StatusLabel { get; init; }

    [JsonPropertyName("shows")]
    public List<JsonElement> Shows { get; init; }

}
