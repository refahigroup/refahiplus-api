using System.Text.Json.Serialization;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Dtos;

public sealed class AabsarTicketDto
{
    [JsonPropertyName("ticket_code")]
    public string TicketCode { get; init; } = string.Empty;

    [JsonPropertyName("isChild")]
    public bool IsChild { get; init; }
}

