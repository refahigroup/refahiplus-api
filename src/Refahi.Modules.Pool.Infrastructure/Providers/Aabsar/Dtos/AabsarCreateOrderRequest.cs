using System.Text.Json.Serialization;

namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Dtos;

// ============================================================================
// Create-order DTOs
// ============================================================================

public sealed class AabsarCreateOrderRequest
{
    [JsonPropertyName("items")]
    public IReadOnlyCollection<AabsarCreateOrderItem> Items { get; init; } = Array.Empty<AabsarCreateOrderItem>();

    [JsonPropertyName("user")]
    public AabsarCreateOrderUser User { get; init; } = new();
}

