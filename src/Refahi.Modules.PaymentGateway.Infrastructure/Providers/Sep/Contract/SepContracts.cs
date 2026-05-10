using System.Text.Json.Serialization;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep.Contract;

/// <summary>
/// درخواست دریافت توکن از SEP
/// POST https://sep.shaparak.ir/onlinepg/onlinepg
/// </summary>
public sealed class SepTokenRequest
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = "Token";

    [JsonPropertyName("TerminalId")]
    public string TerminalId { get; set; } = default!;

    /// <summary>مبلغ به ریال</summary>
    [JsonPropertyName("Amount")]
    public long Amount { get; set; }

    /// <summary>شماره سفارش فروشگاه — ما از SessionId استفاده می‌کنیم</summary>
    [JsonPropertyName("ResNum")]
    public string ResNum { get; set; } = default!;

    /// <summary>آدرس بازگشت بعد از پرداخت (backend callback)</summary>
    [JsonPropertyName("RedirectURL")]
    public string RedirectURL { get; set; } = default!;

    /// <summary>شماره موبایل خریدار جهت پیش‌پر کردن فرم بانک (اختیاری)</summary>
    [JsonPropertyName("CellNumber")]
    public string? CellNumber { get; set; }
}

/// <summary>
/// پاسخ دریافت توکن از SEP
/// Status=1 موفق | مقادیر دیگر خطا
/// </summary>
public sealed class SepTokenResponse
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("errorDesc")]
    public string? ErrorDesc { get; set; }
}

/// <summary>
/// درخواست تأیید تراکنش SEP
/// POST https://sep.shaparak.ir/verifyTxnRandomSessionkey/ipg/VerifyTransaction
/// </summary>
public sealed class SepVerifyRequest
{
    [JsonPropertyName("RefNum")]
    public string RefNum { get; set; } = default!;

    [JsonPropertyName("TerminalNumber")]
    public long TerminalNumber { get; set; }
}

/// <summary>
/// پاسخ تأیید تراکنش SEP
/// ResultCode=0 موفق | مقادیر منفی خطا
/// </summary>
public sealed class SepVerifyResponse
{
    [JsonPropertyName("ResultCode")]
    public int ResultCode { get; set; }

    [JsonPropertyName("Amount")]
    public long Amount { get; set; }

    [JsonPropertyName("TransactionDetail")]
    public SepTransactionDetail? TransactionDetail { get; set; }
}

public sealed class SepTransactionDetail
{
    [JsonPropertyName("MaskedCardNumber")]
    public string? MaskedCardNumber { get; set; }

    [JsonPropertyName("RRN")]
    public string? RRN { get; set; }

    [JsonPropertyName("RefNum")]
    public string? RefNum { get; set; }
}
