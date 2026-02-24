namespace Refahi.Shared.Presentation;

/// <summary>
/// Unified API response for paginated results
/// </summary>
/// <typeparam name="T">Type of items in result</typeparam>
public sealed record PaginatedResponse<T>(
    bool Success,
    IEnumerable<T> Data,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    string Message = "درخواست با موفقیت انجام شد",
    int StatusCode = 200
);

