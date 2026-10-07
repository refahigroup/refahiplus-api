using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Session;

public sealed class SessionResourceCollectionMeta
{
    [JsonPropertyName("message")]
    public string Message { get; init; }

}
