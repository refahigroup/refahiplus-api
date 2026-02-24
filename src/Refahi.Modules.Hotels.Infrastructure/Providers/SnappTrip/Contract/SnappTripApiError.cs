namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

/// <summary>
/// مدل خطاهای SnappTrip — کاملاً مطابق پاسخ‌های واقعی API.
/// </summary>
public sealed class SnappTripApiError
{
    public bool? success { get; set; }
    public string? message { get; set; }
    public string? error { get; set; }
    public string? code { get; set; }
    public string? trace_id { get; set; }
}

