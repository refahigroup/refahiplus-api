#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class SessionResourceCollection
{
    [JsonPropertyName("data")]
    public List<SessionResourceCollectionDataItem> Data { get; init; }

    [JsonPropertyName("meta")]
    public SessionResourceCollectionMeta Meta { get; init; }

}
