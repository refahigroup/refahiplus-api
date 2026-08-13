using System.Text.Json.Serialization;

namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Dtos;

// ============================================================================
// Cancellation DTOs
// ============================================================================

public sealed class AabsarCancelTicketsRequest
{
    [JsonPropertyName("order_code")]
    public string OrderCode { get; init; } = string.Empty;

    [JsonPropertyName("adult")]
    public int Adult { get; init; }

    [JsonPropertyName("child")]
    public int Child { get; init; }
}

