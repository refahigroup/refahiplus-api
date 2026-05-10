namespace Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Config;

public class JibitOptions
{
    /// <summary>کلید API جیبیت</summary>
    public string ApiKey { get; set; } = default!;

    /// <summary>کلید مخفی API جیبیت</summary>
    public string SecretKey { get; set; } = default!;

    /// <summary>آدرس پایه API جیبیت</summary>
    public string BaseUrl { get; set; } = "https://napi.jibit.ir/ppg/v3";

    /// <summary>مدت اعتبار جلسه پرداخت (دقیقه)</summary>
    public int SessionExpiryMinutes { get; set; } = 15;

    /// <summary>Timeout کلی درخواست (ثانیه)</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>تعداد دفعات Retry در صورت بروز خطاهای موقت</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Delay بین Retry ها (میلی‌ثانیه)</summary>
    public int RetryDelayMilliseconds { get; set; } = 500;

    /// <summary>تعداد خطاهای متوالی قبل از Trip شدن Circuit Breaker</summary>
    public int CircuitBreakerFailuresBeforeTrip { get; set; } = 5;

    /// <summary>مدت زمان Open بودن Circuit Breaker (ثانیه)</summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}
