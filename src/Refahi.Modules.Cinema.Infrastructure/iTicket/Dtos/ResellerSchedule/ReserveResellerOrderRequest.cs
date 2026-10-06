#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ReserveResellerOrderRequest
{
    [JsonPropertyName("data")]
    public ReserveResellerOrderRequestData Data { get; init; }

}
