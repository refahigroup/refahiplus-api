using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Checkout;

/// <summary>
/// ثبت سفارش فروشگاه: دریافت اطلاعات Checkout (آدرس، روز ارسال، روش ارسال per-item، کد تخفیف) + پرداخت با کیف‌پول.
/// UserId و ModuleId و IdempotencyKey از Endpoint مقداردهی می‌شوند (با `with`).
/// </summary>
public sealed record PlaceStoreOrderCommand(
    Guid UserId,
    int ModuleId,
    List<WalletPaymentInput> WalletAllocations,
    string IdempotencyKey,
    Guid? ShippingAddressId = null,
    DateOnly? DeliveryDate = null,
    short DeliveryTimeSlot = 0,
    Dictionary<Guid, short>? CartItemDeliveryMethods = null,
    string? DiscountCode = null
) : IRequest<PlaceStoreOrderResponse>;

public sealed record WalletPaymentInput(Guid WalletId, long AmountMinor);

public sealed record PlaceStoreOrderResponse(
    Guid OrderId,
    string OrderNumber,
    long FinalAmountMinor,
    string Status);
