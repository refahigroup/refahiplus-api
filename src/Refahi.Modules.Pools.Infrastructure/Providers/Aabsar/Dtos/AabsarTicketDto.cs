using System.Text.Json.Serialization;

namespace Refahi.Modules.Pools.Infrastructure.Providers.Aabsar.Dtos;

public sealed class AabsarTicketDto
{
    [JsonPropertyName("ticket_code")]
    public string TicketCode { get; init; } = string.Empty;

    [JsonPropertyName("isChild")]
    public bool IsChild { get; init; }
}

