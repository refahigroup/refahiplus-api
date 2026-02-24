namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Config;

public class SnappTripOptions
{
    public string BaseUrl { get; set; } = default!;
    public string ApiKey { get; set; } = default!;

    /// <summary>
    /// Timeout کلی درخواست (برای HttpClient و Polly Timeout)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// تعداد دفعات Retry در صورت بروز خطاهای موقت (5xx, network)
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Delay بین Retry ها (میلی‌ثانیه)
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 300;

    /// <summary>
    /// تعداد خطاهای متوالی قبل از Trip شدن Circuit Breaker
    /// </summary>
    public int CircuitBreakerFailuresBeforeTrip { get; set; } = 5;

    /// <summary>
    /// مدت زمان باز ماندن Circuit Breaker (ثانیه)
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    /// <summary>
    /// حداکثر درخواست‌های هم‌زمان مجاز به SnappTrip
    /// </summary>
    public int BulkheadMaxParallelization { get; set; } = 50;

    /// <summary>
    /// حداکثر صف درخواست‌ها پشت Bulkhead
    /// </summary>
    public int BulkheadMaxQueuedActions { get; set; } = 100;
}