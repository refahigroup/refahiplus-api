namespace Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep.Config;

public class SepOptions
{
    /// <summary>شناسه ترمینال فروشگاه در سامان</summary>
    public string TerminalId { get; set; } = default!;

    /// <summary>آدرس دریافت توکن: POST به این آدرس با پارامتر Action=Token</summary>
    public string TokenUrl { get; set; } = "https://sep.shaparak.ir/onlinepg/onlinepg";

    /// <summary>آدرس پایه صفحه پرداخت SEP (token به query string اضافه می‌شود)</summary>
    public string PaymentBaseUrl { get; set; } = "https://sep.shaparak.ir/OnlinePG/OnlinePG";

    /// <summary>آدرس تأیید تراکنش</summary>
    public string VerifyUrl { get; set; } = "https://sep.shaparak.ir/verifyTxnRandomSessionkey/ipg/VerifyTransaction";

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

    /// <summary>مدت زمان باز ماندن Circuit Breaker (ثانیه)</summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}
