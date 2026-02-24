namespace Refahi.Shared.Presentation;

/// <summary>
/// Unified API response wrapper for all successful responses
/// Frontend-friendly format with consistent structure
/// </summary>
/// <typeparam name="T">Type of response data</typeparam>
public sealed record ApiResponse<T>(
    bool Success,
    T Data,
    string Message = "درخواست با موفقیت انجام شد",
    int StatusCode = 200
);

