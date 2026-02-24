namespace Refahi.Shared.Presentation;

/// <summary>
/// Unified API error response
/// </summary>
public sealed record ApiErrorResponse(
    bool Success = false,
    string Message = "خطایی در پردازش درخواست رخ داد",
    Dictionary<string, string[]>? Errors = null,
    int StatusCode = 400,
    string? TraceId = null
);

