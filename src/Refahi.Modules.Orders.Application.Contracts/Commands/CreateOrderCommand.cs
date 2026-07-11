using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Commands;

/// <summary>
/// ایجاد سفارش جدید — توسط ماژول‌های فروش فراخوانی می‌شود.
/// تمام اطلاعات ارسال (آدرس، روز، روش ارسال per-item) و کد تخفیف اختیاری هستند تا فلوی فعلی ماژول‌های دیگر شکسته نشود.
/// </summary>
public sealed record CreateOrderCommand(
    Guid UserId,
    string SourceModule,
    Guid SourceReferenceId,
    List<CreateOrderItemInput> Items,
    string IdempotencyKey,
    string? ReferenceType = null,
    Guid? ShippingAddressId = null,
    string? ShippingAddressSnapshotJson = null,
    DateOnly? DeliveryDate = null,
    short DeliveryTimeSlot = 0,
    long ShippingFeeMinor = 0,
    string? DiscountCode = null,
    long DiscountCodeAmountMinor = 0,
    Guid? SagaId = null
) : IRequest<CreateOrderResponse>;

public sealed record CreateOrderItemInput(
    string Title,
    long UnitPriceMinor,
    int Quantity,
    long DiscountAmountMinor,
    Guid SourceItemId,
    string CategoryCode,
    string[]? Tags,
    string? MetadataJson,
    short DeliveryMethod = 0);

public sealed record CreateOrderResponse(
    Guid OrderId,
    string OrderNumber,
    long FinalAmountMinor,
    string Currency = "IRR");
