using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class VoucherDelivery
{
    private VoucherDelivery() { }

    public Guid Id { get; private set; }
    public Guid VoucherId { get; private set; }
    public Guid UserId { get; private set; }
    public string Channel { get; private set; } = "Sms";
    public VoucherDeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public uint Version { get; private set; }

    public static VoucherDelivery Queue(Guid voucherId, Guid userId, DateTimeOffset nowUtc)
    {
        if (voucherId == Guid.Empty || userId == Guid.Empty)
            throw new StoreDomainException("اطلاعات ارسال ووچر معتبر نیست", "INVALID_VOUCHER_DELIVERY");
        return new VoucherDelivery
        {
            Id = Guid.NewGuid(),
            VoucherId = voucherId,
            UserId = userId,
            Channel = "Sms",
            Status = VoucherDeliveryStatus.Pending,
            NextAttemptAtUtc = nowUtc,
        };
    }

    public void MarkSent(DateTimeOffset nowUtc)
    {
        Status = VoucherDeliveryStatus.Sent;
        SentAtUtc = nowUtc;
        LastError = null;
    }

    public void MarkNoRecipient() => Status = VoucherDeliveryStatus.SkippedNoRecipient;

    public void MarkFailed(string error, DateTimeOffset nowUtc, int maxRetries)
    {
        AttemptCount++;
        LastError = string.IsNullOrWhiteSpace(error)
            ? "ارسال پیامک ناموفق بود"
            : error.Trim()[..Math.Min(error.Trim().Length, 1000)];
        if (AttemptCount >= maxRetries)
        {
            Status = VoucherDeliveryStatus.DeadLetter;
            return;
        }
        Status = VoucherDeliveryStatus.Retry;
        var delayMinutes = Math.Min(Math.Pow(2, AttemptCount - 1), 360);
        NextAttemptAtUtc = nowUtc.AddMinutes(delayMinutes);
    }
}
