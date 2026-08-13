using System.Net;
using System.Text.Json;

namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Dtos;

// ============================================================================
// Exception type
// ============================================================================

public sealed class AabsarApiException : HttpRequestException
{
    public AabsarApiException(
        string message,
        HttpStatusCode statusCode,
        string? apiMessage,
        JsonElement? validationErrors,
        string? responseBody,
        Exception? innerException = null)
        : base(message, innerException, statusCode)
    {
        ApiMessage = apiMessage;
        ValidationErrors = validationErrors;
        ResponseBody = responseBody;
    }

    /// <summary>
    /// The remote API's "message" field, when available.
    /// </summary>
    public string? ApiMessage { get; }

    /// <summary>
    /// The remote validation "errors" object, when the validation envelope is returned.
    /// </summary>
    public JsonElement? ValidationErrors { get; }

    /// <summary>
    /// Raw response body for diagnostics/logging. Avoid logging secrets from requests.
    /// </summary>
    public string? ResponseBody { get; }
}

