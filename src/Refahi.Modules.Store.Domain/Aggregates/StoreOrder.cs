using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Aggregates;

public sealed class StoreOrder
{
    private readonly List<StoreOrderItem> _items = [];

    private StoreOrder() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public SalesChannel SalesChannel { get; private set; }
    public string SourceModule { get; private set; } = "Store";
    public int ModuleId { get; private set; }
    public Guid ShopId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid? OrderId { get; private set; }
    public StoreOrderStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public long OriginalAmountMinor { get; private set; }
    public long DiscountAmountMinor { get; private set; }
    public long FinalAmountMinor { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public Guid? ShippingAddressId { get; private set; }
    public DateOnly? DeliveryDate { get; private set; }
    public short DeliveryTimeSlot { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string InitiatorType { get; private set; } = "User";
    public string? OtpReferenceCode { get; private set; }
    public DateTimeOffset? OtpExpiresAt { get; private set; }
    public DateTimeOffset? OtpVerifiedAt { get; private set; }
    public DateTimeOffset? OtpDispatchStartedAt { get; private set; }
    public IReadOnlyList<StoreOrderItem> Items => _items.AsReadOnly();

    public static StoreOrder Create(
        Guid userId,
        int moduleId,
        Guid shopId,
        Guid supplierId,
        string idempotencyKey,
        string requestFingerprint,
        IReadOnlyCollection<StoreOrderItemSnapshot> items,
        Guid? shippingAddressId = null,
        DateOnly? deliveryDate = null,
        short deliveryTimeSlot = 0
    )
    {
        if (
            userId == Guid.Empty
            || moduleId <= 0
            || shopId == Guid.Empty
            || supplierId == Guid.Empty
        )
            throw new StoreDomainException(
                "مشخصات سفارش فروشگاه معتبر نیست",
                "INVALID_STORE_ORDER"
            );
        if (
            string.IsNullOrWhiteSpace(idempotencyKey)
            || string.IsNullOrWhiteSpace(requestFingerprint)
            || items.Count == 0
        )
            throw new StoreDomainException(
                "کلید یکتایی و آیتم‌های سفارش الزامی است",
                "INVALID_STORE_ORDER"
            );
        if (items.Any(x => x.ShopId != shopId || x.SupplierId != supplierId))
            throw new StoreDomainException(
                "تمامی محصولات باید از یک فروشگاه باشند",
                "MIXED_SHOP_ITEMS"
            );

        var now = DateTimeOffset.UtcNow;
        var order = new StoreOrder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ModuleId = moduleId,
            ShopId = shopId,
            CreatedByUserId = userId,
            InitiatorType = "User",
            SupplierId = supplierId,
            SalesChannel = SalesChannel.Online,
            SourceModule = "Store",
            IdempotencyKey = idempotencyKey.Trim(),
            Status = StoreOrderStatus.PendingOrder,
            RequestFingerprint = requestFingerprint,
            ShippingAddressId = shippingAddressId,
            DeliveryDate = deliveryDate,
            DeliveryTimeSlot = deliveryTimeSlot,
            CreatedAt = now,
            UpdatedAt = now,
        };
        foreach (var item in items)
            order._items.Add(StoreOrderItem.Create(order.Id, item));
        order.OriginalAmountMinor = order._items.Sum(x =>
            checked(x.OriginalUnitPriceMinor * x.Quantity)
        );
        order.FinalAmountMinor = order._items.Sum(x => x.GrossAmountMinor);
        order.DiscountAmountMinor = order.OriginalAmountMinor - order.FinalAmountMinor;
        return order;
    }

    public static StoreOrder CreateInPerson(
        Guid userId,
        Guid createdByUserId,
        string initiatorType,
        Guid shopId,
        Guid supplierId,
        string idempotencyKey,
        string requestFingerprint,
        StoreOrderItemSnapshot item
    )
    {
        if (
            userId == Guid.Empty
            || createdByUserId == Guid.Empty
            || shopId == Guid.Empty
            || supplierId == Guid.Empty
        )
            throw new StoreDomainException(
                "مشخصات سفارش حضوری معتبر نیست",
                "INVALID_IN_PERSON_ORDER"
            );
        if (
            initiatorType is not ("Vendor" or "User")
            || string.IsNullOrWhiteSpace(idempotencyKey)
            || string.IsNullOrWhiteSpace(requestFingerprint)
        )
            throw new StoreDomainException(
                "مبدا یا کلید یکتای سفارش حضوری معتبر نیست",
                "INVALID_IN_PERSON_ORDER"
            );
        if (
            item.SalesChannel != SalesChannel.InPerson
            || item.ShopId != shopId
            || item.SupplierId != supplierId
            || item.Quantity != 1
            || item.DeclaredGrossAmountMinor <= 0
        )
            throw new StoreDomainException(
                "سفارش حضوری باید دقیقاً یک آیتم با تعداد یک داشته باشد",
                "INVALID_IN_PERSON_ITEM"
            );

        var now = DateTimeOffset.UtcNow;
        var order = new StoreOrder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedByUserId = createdByUserId,
            InitiatorType = initiatorType,
            ModuleId = 0,
            ShopId = shopId,
            SupplierId = supplierId,
            SalesChannel = SalesChannel.InPerson,
            SourceModule = "Store",
            IdempotencyKey = idempotencyKey.Trim(),
            RequestFingerprint = requestFingerprint,
            Status = StoreOrderStatus.PendingOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        order._items.Add(StoreOrderItem.Create(order.Id, item));
        order.OriginalAmountMinor = item.DeclaredGrossAmountMinor.Value;
        order.DiscountAmountMinor = 0;
        order.FinalAmountMinor = item.DeclaredGrossAmountMinor.Value;
        return order;
    }

    public void AttachOtpChallenge(string protectedReference, DateTimeOffset expiresAt)
    {
        if (
            SalesChannel != SalesChannel.InPerson
            || InitiatorType != "Vendor"
            || string.IsNullOrWhiteSpace(protectedReference)
        )
            throw new StoreDomainException("مرجع کد تایید معتبر نیست", "OTP_REFERENCE_INVALID");
        OtpReferenceCode = protectedReference;
        OtpExpiresAt = expiresAt;
        OtpVerifiedAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void BeginOtpDispatch()
    {
        if (
            SalesChannel != SalesChannel.InPerson
            || InitiatorType != "Vendor"
            || !string.IsNullOrWhiteSpace(OtpReferenceCode)
        )
            throw new StoreDomainException(
                "ارسال کد تایید در این وضعیت مجاز نیست",
                "OTP_DISPATCH_INVALID"
            );
        OtpDispatchStartedAt ??= DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkOtpVerified()
    {
        if (
            SalesChannel != SalesChannel.InPerson
            || InitiatorType != "Vendor"
            || string.IsNullOrWhiteSpace(OtpReferenceCode)
        )
            throw new StoreDomainException("چالش کد تایید معتبر نیست", "OTP_CHALLENGE_NOT_FOUND");
        OtpVerifiedAt ??= DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void EnsureRequestFingerprint(string fingerprint)
    {
        if (!string.Equals(RequestFingerprint, fingerprint, StringComparison.Ordinal))
            throw new StoreDomainException(
                "کلید یکتایی با اطلاعات متفاوتی استفاده شده است",
                "IDEMPOTENCY_PAYLOAD_MISMATCH"
            );
    }

    public void AttachOrder(Guid orderId)
    {
        if (orderId == Guid.Empty)
            throw new StoreDomainException("شناسه سفارش معتبر نیست", "INVALID_ORDER_ID");
        if (OrderId.HasValue && OrderId.Value != orderId)
            throw new StoreDomainException(
                "سفارش فروشگاه قبلاً به سفارش دیگری متصل شده است",
                "ORDER_ALREADY_ATTACHED"
            );
        OrderId = orderId;
        Status = StoreOrderStatus.PendingPayment;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkPaid() => Transition(StoreOrderStatus.PendingPayment, StoreOrderStatus.Paid);

    public void MarkCancelled()
    {
        if (Status is StoreOrderStatus.Cancelled or StoreOrderStatus.Refunded)
            return;
        if (Status == StoreOrderStatus.Paid)
            throw new StoreDomainException(
                "سفارش پرداخت‌شده باید بازپرداخت شود",
                "PAID_ORDER_CANNOT_CANCEL"
            );
        Status = StoreOrderStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkRefunded()
    {
        if (Status == StoreOrderStatus.Refunded)
            return;
        if (Status != StoreOrderStatus.Paid)
            throw new StoreDomainException(
                "فقط سفارش پرداخت‌شده قابل بازپرداخت است",
                "INVALID_STORE_ORDER_TRANSITION"
            );
        Status = StoreOrderStatus.Refunded;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed()
    {
        if (Status != StoreOrderStatus.PendingOrder)
            throw new StoreDomainException(
                "وضعیت سفارش قابل تغییر به ناموفق نیست",
                "INVALID_STORE_ORDER_TRANSITION"
            );
        Status = StoreOrderStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void Transition(StoreOrderStatus from, StoreOrderStatus to)
    {
        if (Status == to)
            return;
        if (Status != from)
            throw new StoreDomainException(
                "تغییر وضعیت سفارش فروشگاه مجاز نیست",
                "INVALID_STORE_ORDER_TRANSITION"
            );
        Status = to;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
