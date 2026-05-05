using Refahi.Modules.Orders.Domain.Entities;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Events;
using Refahi.Modules.Orders.Domain.Exceptions;
using Refahi.Shared.Domain;

namespace Refahi.Modules.Orders.Domain.Aggregates;

/// <summary>
/// Order — Aggregate Root
/// سفارش ژنریک: مستقل از ماژول مبدا (Store, Hotel, Flight, ...)
/// تنها چیز قابل پرداخت در سیستم
/// </summary>
public sealed class Order
{
    private Order() { _items = new List<OrderItem>(); _domainEvents = new List<IDomainEvent>(); }

    public Guid Id { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;    // شماره سفارش یکتا (مثل "ORD-240413-XXXX")
    public Guid UserId { get; private set; }                           // FK → Identity (via Contract)

    // --- مبالغ (long / ریال) ---
    public long TotalAmountMinor { get; private set; }                 // جمع کل قبل از تخفیف
    public long DiscountAmountMinor { get; private set; }              // مجموع تخفیفات کالاها
    public long ShippingFeeMinor { get; private set; }                 // هزینه ارسال
    public string? DiscountCode { get; private set; }                  // کد تخفیف اعمال‌شده
    public long DiscountCodeAmountMinor { get; private set; }          // مبلغ کاسته‌شده توسط کد تخفیف
    public long FinalAmountMinor { get; private set; }                 // مبلغ نهایی قابل پرداخت
    public string Currency { get; private set; } = "IRR";

    // --- وضعیت ---
    public OrderStatus Status { get; private set; }
    public PaymentState PaymentState { get; private set; }

    // --- ارتباط با Wallet ---
    public Guid? PaymentIntentId { get; private set; }                 // از Wallets Module
    public Guid? PaymentId { get; private set; }                       // از Wallets Module

    // --- ماژول مبدا ---
    public string SourceModule { get; private set; } = string.Empty;  // "Store", "Hotel", "Flight"
    public Guid SourceReferenceId { get; private set; }               // رفرنس به رکورد اصلی در ماژول مبدا

    // --- اطلاعات ارسال (Snapshot از Identity) ---
    public Guid? ShippingAddressId { get; private set; }              // FK soft → Identity.UserAddress
    public string? ShippingAddressSnapshotJson { get; private set; }  // Snapshot JSON آدرس برای حفظ تاریخچه
    public DateOnly? DeliveryDate { get; private set; }               // روز تحویل
    public DeliveryTimeSlot DeliveryTimeSlot { get; private set; }    // بازه‌ی ساعتی تحویل (فاز ۲)

    // --- Idempotency ---
    public string IdempotencyKey { get; private set; } = string.Empty;

    // --- زمان ---
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // --- Optimistic Concurrency (PostgreSQL xmin) ---
    public uint RowVersion { get; private set; }

    // --- آیتم‌ها ---
    private readonly List<OrderItem> _items;
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    // --- Domain Events ---
    private readonly List<IDomainEvent> _domainEvents;
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void AddDomainEvent(IDomainEvent evt) => _domainEvents.Add(evt);

    // =========================================================
    // Factory Method
    // =========================================================
    public static Order Create(
        Guid userId,
        string sourceModule,
        Guid sourceReferenceId,
        string idempotencyKey,
        List<OrderItemData> items,
        Guid? shippingAddressId = null,
        string? shippingAddressSnapshotJson = null,
        DateOnly? deliveryDate = null,
        DeliveryTimeSlot deliveryTimeSlot = DeliveryTimeSlot.None,
        long shippingFeeMinor = 0,
        string? discountCode = null,
        long discountCodeAmountMinor = 0)
    {
        if (items is null || items.Count == 0)
            throw new OrderDomainException("سفارش باید حداقل یک آیتم داشته باشد", "ORDER_EMPTY");

        if (shippingFeeMinor < 0)
            throw new OrderDomainException("هزینه ارسال نمی‌تواند منفی باشد", "INVALID_SHIPPING_FEE");

        if (discountCodeAmountMinor < 0)
            throw new OrderDomainException("مبلغ کد تخفیف نمی‌تواند منفی باشد", "INVALID_DISCOUNT_CODE_AMOUNT");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = GenerateOrderNumber(),
            UserId = userId,
            Currency = "IRR",
            Status = OrderStatus.Pending,
            PaymentState = PaymentState.Unpaid,
            SourceModule = sourceModule,
            SourceReferenceId = sourceReferenceId,
            IdempotencyKey = idempotencyKey,
            ShippingAddressId = shippingAddressId,
            ShippingAddressSnapshotJson = shippingAddressSnapshotJson,
            DeliveryDate = deliveryDate,
            DeliveryTimeSlot = deliveryTimeSlot,
            ShippingFeeMinor = shippingFeeMinor,
            DiscountCode = string.IsNullOrWhiteSpace(discountCode) ? null : discountCode.Trim(),
            DiscountCodeAmountMinor = discountCodeAmountMinor,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var sortOrder = 0;
        foreach (var item in items)
        {
            order._items.Add(OrderItem.Create(
                orderId: order.Id,
                title: item.Title,
                unitPriceMinor: item.UnitPriceMinor,
                quantity: item.Quantity,
                discountAmountMinor: item.DiscountAmountMinor,
                sourceModule: sourceModule,
                sourceItemId: item.SourceItemId,
                categoryCode: item.CategoryCode,
                tags: item.Tags,
                metadataJson: item.MetadataJson,
                sortOrder: sortOrder++,
                deliveryMethod: item.DeliveryMethod));
        }

        order.RecalculateAmounts();

        order.AddDomainEvent(new OrderCreatedEvent(
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            UserId: order.UserId,
            SourceModule: order.SourceModule,
            SourceReferenceId: order.SourceReferenceId,
            FinalAmountMinor: order.FinalAmountMinor,
            OccurredAt: DateTimeOffset.UtcNow));

        return order;
    }

    // =========================================================
    // Domain Behaviors
    // =========================================================

    /// <summary>
    /// ثبت رزرو مبلغ (PaymentIntent created)
    /// </summary>
    public void MarkAsReserved(Guid paymentIntentId)
    {
        if (PaymentState != PaymentState.Unpaid)
            throw new OrderDomainException("سفارش قبلاً پرداخت/رزرو شده", "ALREADY_RESERVED");

        PaymentIntentId = paymentIntentId;
        PaymentState = PaymentState.Reserved;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// نهایی‌سازی پرداخت (PaymentIntent captured)
    /// </summary>
    public void MarkAsPaid(Guid paymentId)
    {
        if (PaymentState != PaymentState.Reserved)
            throw new OrderDomainException("ابتدا باید مبلغ رزرو شود", "NOT_RESERVED");

        PaymentId = paymentId;
        PaymentState = PaymentState.Paid;
        Status = OrderStatus.Confirmed;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderPaidEvent(
            OrderId: Id,
            OrderNumber: OrderNumber,
            UserId: UserId,
            PaymentId: paymentId,
            AmountMinor: FinalAmountMinor,
            OccurredAt: DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// لغو سفارش
    /// </summary>
    public void Cancel()
    {
        if (Status == OrderStatus.Delivered)
            throw new OrderDomainException("سفارش تحویل شده قابل لغو نیست", "CANNOT_CANCEL_DELIVERED");
        if (Status == OrderStatus.Cancelled)
            throw new OrderDomainException("سفارش قبلاً لغو شده", "ALREADY_CANCELLED");

        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// آزادسازی مبلغ (Release بعد از لغو، قبل از Capture)
    /// </summary>
    public void MarkAsReleased()
    {
        if (PaymentState != PaymentState.Reserved)
            throw new OrderDomainException("فقط مبلغ رزرو شده قابل آزادسازی است", "NOT_RESERVED");

        PaymentState = PaymentState.Released;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// بازگشت وجه (Refund بعد از Capture)
    /// </summary>
    public void MarkAsRefunded()
    {
        if (PaymentState != PaymentState.Paid)
            throw new OrderDomainException("فقط سفارش پرداخت‌شده قابل بازگشت وجه است", "NOT_PAID");

        PaymentState = PaymentState.Refunded;
        Status = OrderStatus.Refunded;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// تغییر وضعیت توسط تامین‌کننده/ادمین
    /// </summary>
    public void UpdateStatus(OrderStatus newStatus)
    {
        var allowed = (Status, newStatus) switch
        {
            (OrderStatus.Confirmed, OrderStatus.Processing) => true,
            (OrderStatus.Processing, OrderStatus.Shipped) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
            (OrderStatus.Processing, OrderStatus.Cancelled) => true,
            _ => false
        };

        if (!allowed)
            throw new OrderDomainException(
                $"تغییر وضعیت از {Status} به {newStatus} مجاز نیست",
                "INVALID_STATUS_TRANSITION");

        var oldStatus = Status.ToString();
        Status = newStatus;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderStatusChangedEvent(
            OrderId: Id,
            OrderNumber: OrderNumber,
            OldStatus: oldStatus,
            NewStatus: newStatus.ToString(),
            OccurredAt: DateTimeOffset.UtcNow));

        if (newStatus == OrderStatus.Delivered)
        {
            AddDomainEvent(new OrderDeliveredEvent(
                OrderId: Id,
                OrderNumber: OrderNumber,
                UserId: UserId,
                SourceModule: SourceModule,
                SourceReferenceId: SourceReferenceId,
                OccurredAt: DateTimeOffset.UtcNow));
        }
    }

    // =========================================================
    // Private Helpers
    // =========================================================

    private void RecalculateAmounts()
    {
        TotalAmountMinor = _items.Sum(i => i.UnitPriceMinor * i.Quantity);
        DiscountAmountMinor = _items.Sum(i => i.DiscountAmountMinor);
        // فرمول نهایی: قیمت کل - تخفیف کالاها - تخفیف کد + هزینه ارسال
        var final = TotalAmountMinor - DiscountAmountMinor - DiscountCodeAmountMinor + ShippingFeeMinor;
        if (final < 0) final = 0;
        FinalAmountMinor = final;
    }

    private static string GenerateOrderNumber()
    {
        var datePart = DateTime.UtcNow.ToString("yyMMdd");
        var randomPart = Guid.NewGuid().ToString("N")[..6].ToUpper();
        return $"ORD-{datePart}-{randomPart}";
    }
}

/// <summary>
/// Input data for creating OrderItem (used in Factory)
/// </summary>
public sealed record OrderItemData(
    string Title,
    long UnitPriceMinor,
    int Quantity,
    long DiscountAmountMinor,
    Guid SourceItemId,
    string CategoryCode,
    string[]? Tags,
    string? MetadataJson,
    DeliveryMethod DeliveryMethod = DeliveryMethod.None);
