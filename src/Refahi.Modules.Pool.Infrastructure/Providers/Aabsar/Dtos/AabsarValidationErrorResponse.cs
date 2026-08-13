using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Dtos;

/// <summary>
/// Validation errors use a different envelope in the Aabsar API.
/// The structure inside "errors" is not specified by the provided API document,
/// therefore it is intentionally represented as JsonElement.
/// </summary>
public sealed class AabsarValidationErrorResponse
{
    [JsonPropertyName("status")]
    public bool Status { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("errors")]
    public JsonElement? Errors { get; init; }
}

