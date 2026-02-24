namespace Refahi.Shared.Presentation;

/// <summary>
/// Helper class for creating standard responses
/// </summary>
public static class ApiResponseHelper
{
    /// <summary>
    /// Create a successful response
    /// </summary>
    public static ApiResponse<T> Success<T>(T data, string message = "درخواست با موفقیت انجام شد", int statusCode = 200)
        => new(true, data, message, statusCode);

    /// <summary>
    /// Create a paginated response
    /// </summary>
    public static PaginatedResponse<T> SuccessPaginated<T>(
        IEnumerable<T> data,
        int pageNumber,
        int pageSize,
        int totalCount,
        string message = "درخواست با موفقیت انجام شد",
        int statusCode = 200)
    {
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new(true, data, pageNumber, pageSize, totalCount, totalPages, message, statusCode);
    }
    public static ApiErrorResponse Error(
        string message = "خطایی در پردازش درخواست رخ داد",
        Dictionary<string, string[]>? errors = null,
        int statusCode = 400,
        string? traceId = null)
        => new(false, message, errors, statusCode, traceId);

    /// <summary>
    /// Create a validation error response
    /// </summary>
    public static ApiErrorResponse ValidationError(
        Dictionary<string, string[]> errors,
        int statusCode = 400)
        => new(false, "اعتبارسنجی ناموفق بود", errors, statusCode);
}

