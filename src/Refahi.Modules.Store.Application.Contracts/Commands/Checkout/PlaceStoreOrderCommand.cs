using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Checkout;

/// <summary>
/// آماده‌سازی سفارش فروشگاه و تحویل آن به Checkout مشترک Orders.
/// انتخاب کیف‌پول و پرداخت فقط در ماژول Orders انجام می‌شود.
/// </summary>
public sealed record PlaceStoreOrderCommand(
    Guid UserId,
    int ModuleId,
    string IdempotencyKey,
    Guid? ShippingAddressId = null,
    DateOnly? DeliveryDate = null,
    short DeliveryTimeSlot = 0,
    Dictionary<Guid, short>? CartItemDeliveryMethods = null,
    string? DiscountCode = null
) : IRequest<PlaceStoreOrderResponse>;

public sealed record PlaceStoreOrderResponse(
    Guid OrderId,
    string OrderNumber,
    long FinalAmountMinor,
    string Status);
