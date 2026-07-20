using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Domain.Aggregates;

public sealed class ChargeRequest
{
    public const int ProviderInvoiceNumberMaxLength = 25;
    private const string ProviderInvoiceNumberPrefix = "CHG";
    private readonly List<ChargeFulfillmentAttempt> _attempts = [];
    private readonly List<ChargePin> _pins = [];
    private ChargeRequest() { }

    public Guid Id { get; private set; }
    public Guid SagaId { get; private set; }
    public Guid UserId { get; private set; }
    public string ProviderName { get; private set; } = string.Empty;
    public ChargeOperator Operator { get; private set; }
    public ChargeServiceType ServiceType { get; private set; }
    public string DestinationMobileNumber { get; private set; } = string.Empty;
    public string? OriginMobileNumber { get; private set; }
    public string ProviderProductId { get; private set; } = string.Empty;
    public string ProductCaption { get; private set; } = string.Empty;
    public int ProductCategory { get; private set; }
    public int PayBill { get; private set; }
    public int? PinCategoryId { get; private set; }
    public int PinCount { get; private set; }
    public string ProductSnapshotJson { get; private set; } = "{}";
    public long ProviderCostMinor { get; private set; }
    public Guid? MarkupRuleId { get; private set; }
    public decimal MarkupPercent { get; private set; }
    public long MarkupFixedMinor { get; private set; }
    public long MarkupAmountMinor { get; private set; }
    public long FinalAmountMinor { get; private set; }
    public string Currency { get; private set; } = "IRR";
    public ChargeRequestStatus Status { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public string CustomerInvoiceNumber { get; private set; } = string.Empty;
    public string? ProviderRrn { get; private set; }
    public string? ProviderTraceId { get; private set; }
    public int? EniacResultCode { get; private set; }
    public string? OperatorResultCode { get; private set; }
    public string? ProviderMessage { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime ExpireAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? FulfilledAt { get; private set; }
    public DateTime? NextReconciliationAt { get; private set; }
    public int ReconciliationCount { get; private set; }
    public DateTime? ProcessingLeaseUntil { get; private set; }
    public string? ProcessingLeaseOwner { get; private set; }
    public string? RefundIdempotencyKey { get; private set; }
    public string? RefundReason { get; private set; }
    public DateTime? RefundStartedAt { get; private set; }
    public DateTime? RefundLastAttemptAt { get; private set; }
    public int RefundAttemptCount { get; private set; }
    public string? RefundLastError { get; private set; }
    public uint RowVersion { get; private set; }
    public IReadOnlyCollection<ChargeFulfillmentAttempt> Attempts => _attempts.AsReadOnly();
    public IReadOnlyCollection<ChargePin> Pins => _pins.AsReadOnly();

    public static ChargeRequest Create(Guid userId, string providerName, ChargeOperator @operator,
        ChargeServiceType serviceType, string destinationMobileNumber, string? originMobileNumber,
        string providerProductId, string productCaption, int productCategory, int payBill,
        int? pinCategoryId, int pinCount, string productSnapshotJson, long providerCostMinor,
        Guid? markupRuleId, decimal markupPercent, long markupFixedMinor, long markupAmountMinor,
        long finalAmountMinor, string idempotencyKey, DateTime nowUtc, DateTime expireAtUtc)
    {
        if (userId == Guid.Empty)
            throw new InvalidOperationException("شناسه کاربر الزامی است");

        if (!Enum.IsDefined(@operator))
            throw new InvalidOperationException("اپراتور معتبر نیست");

        if (!Enum.IsDefined(serviceType))
            throw new InvalidOperationException("نوع خدمت معتبر نیست");

        if (string.IsNullOrWhiteSpace(providerName))
            throw new InvalidOperationException("تامین‌کننده الزامی است");

        if (string.IsNullOrWhiteSpace(destinationMobileNumber))
            throw new InvalidOperationException("شماره مقصد الزامی است");

        if (providerCostMinor <= 0 || finalAmountMinor <= 0)
            throw new InvalidOperationException("مبلغ خرید معتبر نیست");

        if (markupAmountMinor < 0 || markupFixedMinor < 0 || markupPercent < 0)
            throw new InvalidOperationException("مبلغ افزایش قیمت معتبر نیست");

        if (expireAtUtc <= nowUtc)
            throw new InvalidOperationException("زمان انقضا معتبر نیست");

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new InvalidOperationException("کلید تکرارپذیری الزامی است");

        var id = Guid.NewGuid();
        return new ChargeRequest
        {
            Id = id,
            SagaId = Guid.NewGuid(),
            UserId = userId,
            ProviderName = providerName.Trim(),
            Operator = @operator,
            ServiceType = serviceType,
            DestinationMobileNumber = destinationMobileNumber.Trim(),
            OriginMobileNumber = string.IsNullOrWhiteSpace(originMobileNumber) ? null : originMobileNumber.Trim(),
            ProviderProductId = providerProductId.Trim(),
            ProductCaption = productCaption.Trim(),
            ProductCategory = productCategory,
            PayBill = payBill,
            PinCategoryId = pinCategoryId,
            PinCount = pinCount,
            ProductSnapshotJson = string.IsNullOrWhiteSpace(productSnapshotJson) ? "{}" : productSnapshotJson,
            ProviderCostMinor = providerCostMinor,
            MarkupRuleId = markupRuleId,
            MarkupPercent = markupPercent,
            MarkupFixedMinor = markupFixedMinor,
            MarkupAmountMinor = markupAmountMinor,
            FinalAmountMinor = finalAmountMinor,
            CustomerInvoiceNumber = CreateProviderInvoiceNumber(id),
            IdempotencyKey = idempotencyKey.Trim(),
            Status = ChargeRequestStatus.Created,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            ExpireAt = expireAtUtc
        };
    }

    private static string CreateProviderInvoiceNumber(Guid id)
    {
        var suffixLength = ProviderInvoiceNumberMaxLength - ProviderInvoiceNumberPrefix.Length;
        var suffix = id.ToString("N")[..suffixLength];
        return $"{ProviderInvoiceNumberPrefix}{suffix}";
    }

    public void ConvertToOrder(Guid orderId, DateTime nowUtc)
    {
        if (Status == ChargeRequestStatus.ConvertedToOrder && OrderId == orderId)
            return;

        if (Status != ChargeRequestStatus.Created)
            throw new InvalidOperationException("درخواست شارژ در وضعیت قابل تبدیل به سفارش نیست");

        if (ExpireAt <= nowUtc)
        {
            Status = ChargeRequestStatus.Expired; UpdatedAt = nowUtc;
            throw new InvalidOperationException("مهلت درخواست شارژ به پایان رسیده است");
        }

        OrderId = orderId;
        Status = ChargeRequestStatus.ConvertedToOrder;
        UpdatedAt = nowUtc;
    }

    public void MarkPaid(Guid orderId, Guid paymentId, DateTime nowUtc)
    {
        if (OrderId.HasValue && OrderId != orderId)
            throw new InvalidOperationException("سفارش با درخواست شارژ مطابقت ندارد");

        bool isInValid = Status is
            ChargeRequestStatus.Paid or
            ChargeRequestStatus.Processing or
            ChargeRequestStatus.ReconciliationPending or
            ChargeRequestStatus.Fulfilled;

        if (isInValid)
            return;

        if (Status != ChargeRequestStatus.ConvertedToOrder)
            throw new InvalidOperationException("درخواست شارژ هنوز به سفارش تبدیل نشده است");

        OrderId = orderId;
        PaymentId = paymentId;
        PaidAt = nowUtc;
        Status = ChargeRequestStatus.Paid;
        UpdatedAt = nowUtc;
    }

    public void StartProcessing(string leaseOwner, DateTime nowUtc, TimeSpan leaseDuration)
    {
        if (Status == ChargeRequestStatus.Processing && ProcessingLeaseUntil > nowUtc)
            throw new InvalidOperationException("درخواست شارژ در حال پردازش است");

        bool isInValid = Status is
            not ChargeRequestStatus.Paid and
            not ChargeRequestStatus.ReconciliationPending and
            not ChargeRequestStatus.Processing;

        if (isInValid)
            throw new InvalidOperationException("درخواست شارژ قابل پردازش نیست");

        // Requests created before the Eniac 25-character contract fix are normalized
        // only before their first provider call. Reconciliation must keep the original value.
        if (Status == ChargeRequestStatus.Paid && CustomerInvoiceNumber.Length > ProviderInvoiceNumberMaxLength)
            CustomerInvoiceNumber = CreateProviderInvoiceNumber(Id);

        Status = ChargeRequestStatus.Processing;
        ProcessingLeaseOwner = leaseOwner;
        ProcessingLeaseUntil = nowUtc.Add(leaseDuration);
        UpdatedAt = nowUtc;
    }

    public void RecordAttempt(ChargeFulfillmentAttempt attempt) => _attempts.Add(attempt);

    public void MarkReconciliationPending(int? eniacCode, string? operatorCode, string? message, DateTime nextAttemptUtc, DateTime nowUtc)
    {
        Status = ChargeRequestStatus.ReconciliationPending;
        EniacResultCode = eniacCode;
        OperatorResultCode = operatorCode;
        ProviderMessage = message;
        NextReconciliationAt = nextAttemptUtc;
        ReconciliationCount++;
        ReleaseLease(nowUtc);
    }

    public void MarkFulfilled(string? rrn, string? traceId, int? eniacCode, string? operatorCode, string? message, DateTime nowUtc)
    {
        ProviderRrn = rrn;
        ProviderTraceId = traceId;
        EniacResultCode = eniacCode;
        OperatorResultCode = operatorCode;
        ProviderMessage = message;
        Status = ChargeRequestStatus.Fulfilled;
        FulfilledAt = nowUtc;
        NextReconciliationAt = null;
        ReleaseLease(nowUtc);
    }

    public void AddPin(string encryptedSerial, string encryptedCode, long amountMinor)
        => _pins.Add(ChargePin.Create(Id, encryptedSerial, encryptedCode, amountMinor, DateTime.UtcNow));

    public void MarkFailed(int? eniacCode, string? operatorCode, string? message, DateTime nowUtc)
    {
        EniacResultCode = eniacCode;
        OperatorResultCode = operatorCode;
        ProviderMessage = message;
        Status = ChargeRequestStatus.Failed;
        NextReconciliationAt = null;
        ReleaseLease(nowUtc);
    }

    public void BeginRefund(string reason, string idempotencyKey, DateTime nowUtc)
    {
        if (Status == ChargeRequestStatus.Refunded)
            return;

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("دلیل بازگشت وجه الزامی است");

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new InvalidOperationException("کلید تکرارپذیری بازگشت وجه الزامی است");

        if (Status == ChargeRequestStatus.Refunding)
        {
            RefundReason ??= reason.Trim();
            RefundIdempotencyKey ??= idempotencyKey.Trim();
            RefundStartedAt ??= nowUtc;
            UpdatedAt = nowUtc;
            return;
        }

        if (Status is not ChargeRequestStatus.Paid
            and not ChargeRequestStatus.Processing
            and not ChargeRequestStatus.ReconciliationPending
            and not ChargeRequestStatus.Failed
            and not ChargeRequestStatus.ManualReview)
            throw new InvalidOperationException("درخواست شارژ در وضعیت قابل بازگشت وجه نیست");

        Status = ChargeRequestStatus.Refunding;
        RefundReason = reason.Trim();
        RefundIdempotencyKey = idempotencyKey.Trim();
        RefundStartedAt = nowUtc;
        RefundLastError = null;
        NextReconciliationAt = null;
        ReleaseLease(nowUtc);
    }

    public void StartRefundAttempt(string leaseOwner, DateTime nowUtc, TimeSpan leaseDuration)
    {
        if (Status != ChargeRequestStatus.Refunding)
            throw new InvalidOperationException("درخواست شارژ در وضعیت بازگشت وجه نیست");

        if (ProcessingLeaseUntil > nowUtc)
            throw new InvalidOperationException("بازگشت وجه درخواست شارژ در حال پردازش است");

        if (string.IsNullOrWhiteSpace(RefundIdempotencyKey) || string.IsNullOrWhiteSpace(RefundReason))
            throw new InvalidOperationException("اطلاعات بازیابی بازگشت وجه کامل نیست");

        ProcessingLeaseOwner = leaseOwner;
        ProcessingLeaseUntil = nowUtc.Add(leaseDuration);
        RefundLastAttemptAt = nowUtc;
        RefundAttemptCount++;
        RefundLastError = null;
        UpdatedAt = nowUtc;
    }

    public void MarkRefundAttemptFailed(string error, DateTime nextAttemptUtc, DateTime nowUtc)
    {
        if (Status != ChargeRequestStatus.Refunding)
            return;

        RefundLastError = string.IsNullOrWhiteSpace(error)
            ? "اجرای بازگشت وجه ناموفق بود"
            : error.Trim()[..Math.Min(error.Trim().Length, 2000)];
        NextReconciliationAt = nextAttemptUtc;
        ReleaseLease(nowUtc);
    }

    public void MarkRefunded(DateTime nowUtc)
    {
        Status = ChargeRequestStatus.Refunded;
        NextReconciliationAt = null;
        RefundLastError = null;
        ReleaseLease(nowUtc);
    }

    public void MarkManualReview(string? message, DateTime nowUtc)
    {
        ProviderMessage = message;
        Status = ChargeRequestStatus.ManualReview;
        NextReconciliationAt = null;
        ReleaseLease(nowUtc);
    }

    public void ConfirmFulfilledByAdmin(string rrn, string traceId, string evidence, DateTime nowUtc)
    {
        if (Status is not ChargeRequestStatus.ManualReview and not ChargeRequestStatus.ReconciliationPending)
            throw new InvalidOperationException("درخواست شارژ در وضعیت قابل تایید دستی نیست");
        if (string.IsNullOrWhiteSpace(rrn) || string.IsNullOrWhiteSpace(traceId) || string.IsNullOrWhiteSpace(evidence))
            throw new InvalidOperationException("شناسه‌های تامین‌کننده و مستند تایید الزامی است");
        MarkFulfilled(rrn.Trim(), traceId.Trim(), EniacResultCode, OperatorResultCode,
            $"تایید دستی: {evidence.Trim()}", nowUtc);
    }

    public void MarkExpired(DateTime nowUtc)
    {
        if (Status != ChargeRequestStatus.Created)
            return;

        Status = ChargeRequestStatus.Expired;
        UpdatedAt = nowUtc;
    }

    public void MarkExpiredAfterOrderClosed(DateTime nowUtc)
    {
        if (Status is not ChargeRequestStatus.Created and not ChargeRequestStatus.ConvertedToOrder)
            return;
        Status = ChargeRequestStatus.Expired;
        NextReconciliationAt = null;
        UpdatedAt = nowUtc;
    }

    public void Cancel(DateTime nowUtc)
    {
        if (Status == ChargeRequestStatus.Cancelled)
            return;

        if (Status != ChargeRequestStatus.Created)
            throw new InvalidOperationException("فقط درخواست ایجادشده قابل لغو است");

        Status = ChargeRequestStatus.Cancelled;
        UpdatedAt = nowUtc;
    }

    private void ReleaseLease(DateTime nowUtc)
    {
        ProcessingLeaseOwner = null;
        ProcessingLeaseUntil = null;
        UpdatedAt = nowUtc;
    }
}
