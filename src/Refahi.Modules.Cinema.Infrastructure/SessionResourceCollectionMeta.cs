#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class SessionResourceCollectionMeta
{
    [JsonPropertyName("message")]
    public string Message { get; init; }

}
