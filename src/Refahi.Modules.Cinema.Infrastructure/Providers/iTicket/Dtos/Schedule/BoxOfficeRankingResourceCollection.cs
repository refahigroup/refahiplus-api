using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Schedule;

public sealed class BoxOfficeRankingResourceCollection
{
    [JsonPropertyName("data")]
    public List<string> Data { get; init; }

}
