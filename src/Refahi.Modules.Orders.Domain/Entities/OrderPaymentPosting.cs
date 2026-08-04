using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Exceptions;

namespace Refahi.Modules.Orders.Domain.Entities;

public sealed class OrderPaymentPosting
{
    private OrderPaymentPosting() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid WalletId { get; private set; }
    public PaymentPostingDirection Direction { get; private set; }
    public long AmountMinor { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    internal static OrderPaymentPosting Create(Guid orderId, Guid walletId,
        PaymentPostingDirection direction, long amountMinor, string purpose, int sortOrder)
    {
        if (walletId == Guid.Empty)
            throw new OrderDomainException("شناسه کیف مقصد معتبر نیست", "INVALID_POSTING_WALLET");
        if (amountMinor <= 0)
            throw new OrderDomainException("مبلغ ثبت مالی باید مثبت باشد", "INVALID_POSTING_AMOUNT");
        if (string.IsNullOrWhiteSpace(purpose))
            throw new OrderDomainException("هدف ثبت مالی الزامی است", "INVALID_POSTING_PURPOSE");

        return new OrderPaymentPosting
        {
            Id = Guid.NewGuid(), OrderId = orderId, WalletId = walletId,
            Direction = direction, AmountMinor = amountMinor,
            Purpose = purpose.Trim(), SortOrder = sortOrder
        };
    }
}
