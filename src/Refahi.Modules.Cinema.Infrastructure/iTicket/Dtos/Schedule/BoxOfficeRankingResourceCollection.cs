#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket.Dtos.Schedule;

public sealed class BoxOfficeRankingResourceCollection
{
    [JsonPropertyName("data")]
    public List<string> Data { get; init; }

}
