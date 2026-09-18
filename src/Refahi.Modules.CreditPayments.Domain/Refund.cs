namespace Refahi.Modules.CreditPayments.Domain;

public enum RefundStatus : short
{
    Created = 1,
    Processing = 2,
    PendingRecovery = 3,
    Succeeded = 4,
    Failed = 5
}

public enum OrderRefundProjectionStatus : short
{
    NotRequired = 1,
    Pending = 2,
    Completed = 3
}

public sealed class Refund
{
    private Refund() { }

    public Guid Id { get; private set; }
    public Guid GatewayPaymentId { get; private set; }
    public Guid TerminalId { get; private set; }
    public long AmountMinor { get; private set; }
    public string Currency { get; private set; } = "IRR";
    public string? MerchantRefundReference { get; private set; }
    public string? Reason { get; private set; }
    public RefundStatus Status { get; private set; }
    public string? NormalizedFailureCode { get; private set; }
    public Guid? WalletRefundId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessingStartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? LastCheckedAt { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public OrderRefundProjectionStatus OrderProjectionStatus { get; private set; }
    public int OrderProjectionRetryCount { get; private set; }
    public DateTimeOffset? OrderProjectionNextRetryAt { get; private set; }
    public DateTimeOffset? OrderRefundNotifiedAt { get; private set; }
    public long Version { get; private set; }

    public string WalletIdempotencyKey => $"cpg-refund-{Id:N}";

    public static Refund Claim(Guid paymentId, Guid terminalId, long amountMinor,
        string currency, string? merchantReference, string? reason, DateTimeOffset now)
    {
        if (paymentId == Guid.Empty || terminalId == Guid.Empty || amountMinor <= 0 || currency != "IRR")
            throw new CreditPaymentsDomainException("درخواست بازگشت وجه نامعتبر است");
        merchantReference = Normalize(merchantReference, 100, "مرجع بازگشت وجه نامعتبر است");
        reason = Normalize(reason, 250, "دلیل بازگشت وجه نامعتبر است");
        return new Refund
        {
            Id = Guid.NewGuid(), GatewayPaymentId = paymentId, TerminalId = terminalId,
            AmountMinor = amountMinor, Currency = currency,
            MerchantRefundReference = merchantReference, Reason = reason,
            Status = RefundStatus.Processing, CreatedAt = now, ProcessingStartedAt = now,
            OrderProjectionStatus = OrderRefundProjectionStatus.NotRequired, Version = 1
        };
    }

    public void MarkPendingRecovery(DateTimeOffset now, DateTimeOffset nextRetryAt)
    {
        if (Status is not (RefundStatus.Processing or RefundStatus.PendingRecovery)) Invalid();
        Status = RefundStatus.PendingRecovery; LastCheckedAt = now; NextRetryAt = nextRetryAt;
        RetryCount++; ClearLease(); Version++;
    }

    public void MarkSucceeded(Guid walletRefundId, bool fullRefund, DateTimeOffset completedAt, DateTimeOffset now)
    {
        if (Status is not (RefundStatus.Processing or RefundStatus.PendingRecovery)) Invalid();
        if (walletRefundId == Guid.Empty) throw new CreditPaymentsDomainException("مرجع بازگشت وجه کیف‌پول نامعتبر است");
        WalletRefundId = walletRefundId; Status = RefundStatus.Succeeded;
        CompletedAt = completedAt; LastCheckedAt = now; NextRetryAt = null;
        OrderProjectionStatus = fullRefund ? OrderRefundProjectionStatus.Pending : OrderRefundProjectionStatus.NotRequired;
        OrderProjectionNextRetryAt = fullRefund ? now : null; ClearLease(); Version++;
    }

    public void MarkFailed(string code, DateTimeOffset now)
    {
        if (Status is not (RefundStatus.Processing or RefundStatus.PendingRecovery)) Invalid();
        Status = RefundStatus.Failed; NormalizedFailureCode = CreditPaymentsText.Required(code, 100, "کد خطا نامعتبر است");
        CompletedAt = now; LastCheckedAt = now; NextRetryAt = null; ClearLease(); Version++;
    }

    public void ClaimLease(string owner, DateTimeOffset until, DateTimeOffset now)
    {
        if (Status is not (RefundStatus.Processing or RefundStatus.PendingRecovery) &&
            !(Status == RefundStatus.Succeeded && OrderProjectionStatus == OrderRefundProjectionStatus.Pending)) Invalid();
        LeaseOwner = CreditPaymentsText.Required(owner, 100, "مالک lease نامعتبر است"); LeaseUntil = until;
        LastCheckedAt = now; Version++;
    }

    public void CompleteOrderProjection(DateTimeOffset now)
    {
        if (Status != RefundStatus.Succeeded || OrderProjectionStatus != OrderRefundProjectionStatus.Pending) Invalid();
        OrderProjectionStatus = OrderRefundProjectionStatus.Completed; OrderRefundNotifiedAt = now;
        OrderProjectionNextRetryAt = null; ClearLease(); Version++;
    }

    public void ScheduleOrderProjection(DateTimeOffset now, DateTimeOffset nextRetryAt)
    {
        if (Status != RefundStatus.Succeeded || OrderProjectionStatus != OrderRefundProjectionStatus.Pending) Invalid();
        OrderProjectionRetryCount++; OrderProjectionNextRetryAt = nextRetryAt; LastCheckedAt = now; ClearLease(); Version++;
    }

    private void ClearLease() { LeaseOwner = null; LeaseUntil = null; }
    private static void Invalid() => throw new CreditPaymentsDomainException("تغییر وضعیت بازگشت وجه مجاز نیست");
    private static string? Normalize(string? value, int max, string message)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        if (normalized.Length > max) throw new CreditPaymentsDomainException(message);
        return normalized;
    }
}
