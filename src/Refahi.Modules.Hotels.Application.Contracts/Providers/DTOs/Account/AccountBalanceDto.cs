namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Account;

/// <summary>
/// موجودی حساب پروایدر
/// </summary>
public sealed class AccountBalanceDto
{
    /// <summary>
    /// مبلغ موجود به ریال
    /// </summary>
    public long AvailableBalance { get; set; }

    /// <summary>
    /// مبلغ قفل شده در رزروهای فعال به ریال
    /// </summary>
    public long LockedBalance { get; set; }

    /// <summary>
    /// کل موجودی (AvailableBalance + LockedBalance)
    /// </summary>
    public long TotalBalance => AvailableBalance + LockedBalance;

    /// <summary>
    /// واحد پول
    /// </summary>
    public string Currency { get; set; } = "IRR";

    /// <summary>
    /// تاریخ آخرین بروزرسانی
    /// </summary>
    public DateTime LastUpdated { get; set; }
}
