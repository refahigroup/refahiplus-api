#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ResellerOrderCanceledResourceAttributes
{
    [JsonPropertyName("code")]
    public string Code { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; }

}
