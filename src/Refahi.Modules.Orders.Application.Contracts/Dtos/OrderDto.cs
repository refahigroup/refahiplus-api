namespace Refahi.Modules.Orders.Application.Contracts.Dtos;

public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid UserId,
    long TotalAmountMinor,
    long DiscountAmountMinor,
    long ShippingFeeMinor,
    string? DiscountCode,
    long DiscountCodeAmountMinor,
    long FinalAmountMinor,
    string Status,
    string PaymentState,
    string SourceModule,
    Guid SourceReferenceId,
    Guid? ShippingAddressId,
    string? ShippingAddressSnapshotJson,
    DateOnly? DeliveryDate,
    short DeliveryTimeSlot,
    List<OrderItemDto> Items,
    DateTimeOffset CreatedAt);

public sealed record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    long FinalAmountMinor,
    string Status,
    string SourceModule,
    int ItemCount,
    DateTimeOffset CreatedAt);
