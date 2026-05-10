using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Contract;

/// <summary>
/// درخواست دریافت توکن جدید از جیبیت
/// POST https://napi.jibit.ir/ppg/v3/tokens
/// </summary>
public sealed class JibitTokenRequest
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = default!;

    [JsonPropertyName("secretKey")]
    public string SecretKey { get; set; } = default!;
}

/// <summary>
/// پاسخ دریافت توکن از جیبیت
/// </summary>
public sealed class JibitTokenResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = default!;

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = default!;

    [JsonPropertyName("errors")]
    public List<JibitError>? Errors { get; set; }
}

/// <summary>
/// درخواست Refresh توکن
/// POST https://napi.jibit.ir/ppg/v3/tokens/refresh
/// </summary>
public sealed class JibitRefreshTokenRequest
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = default!;

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = default!;
}

/// <summary>
/// درخواست ایجاد تراکنش جدید در جیبیت
/// POST https://napi.jibit.ir/ppg/v3/purchases
/// </summary>
public sealed class JibitCreatePurchaseRequest
{
    /// <summary>مبلغ به ریال</summary>
    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    /// <summary>آدرس بازگشت بعد از پرداخت</summary>
    [JsonPropertyName("callbackUrl")]
    public string CallbackUrl { get; set; } = default!;

    /// <summary>شماره مرجع سفارش فروشگاه — ما از SessionId استفاده می‌کنیم</summary>
    [JsonPropertyName("clientReferenceNumber")]
    public string ClientReferenceNumber { get; set; } = default!;

    /// <summary>واحد پول — همیشه IRR</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "IRR";

    /// <summary>شناسه کاربر (شماره موبایل یا آیدی کاربر)</summary>
    [JsonPropertyName("userIdentifier")]
    public string UserIdentifier { get; set; } = default!;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("additionalData")]
    public string? AdditionalData { get; set; }
}

/// <summary>
/// پاسخ ایجاد تراکنش از جیبیت
/// </summary>
public sealed class JibitCreatePurchaseResponse
{
    /// <summary>شناسه تراکنش در جیبیت — برای Verify استفاده می‌شود</summary>
    [JsonPropertyName("purchaseId")]
    public string? PurchaseId { get; set; }

    /// <summary>آدرس صفحه پرداخت بانک — مستقیماً redirect می‌دهیم</summary>
    [JsonPropertyName("pspSwitchingUrl")]
    public string? PspSwitchingUrl { get; set; }

    [JsonPropertyName("errors")]
    public List<JibitError>? Errors { get; set; }
}

/// <summary>
/// پاسخ تأیید تراکنش از جیبیت
/// GET https://napi.jibit.ir/ppg/v3/purchases/{purchaseId}/verify
/// </summary>
public sealed class JibitVerifyResponse
{
    /// <summary>وضعیت تراکنش: SUCCESSFUL / FAILED / PURCHASE_NOT_FOUND</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("errors")]
    public List<JibitError>? Errors { get; set; }
}

/// <summary>
/// خطای API جیبیت
/// </summary>
public sealed class JibitError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = default!;

    [JsonPropertyName("message")]
    public string Message { get; set; } = default!;
}
