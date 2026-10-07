using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Session;

public sealed class SessionResourceCollection
{
    [JsonPropertyName("data")]
    public List<SessionResourceCollectionDataItem> Data { get; init; }

    [JsonPropertyName("meta")]
    public SessionResourceCollectionMeta Meta { get; init; }

}
