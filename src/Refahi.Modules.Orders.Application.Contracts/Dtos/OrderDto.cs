namespace Refahi.Modules.Orders.Application.Contracts.Dtos;

public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid UserId,
    long TotalAmountMinor,
    long DiscountAmountMinor,
    long FinalAmountMinor,
    string Status,
    string PaymentState,
    string SourceModule,
    Guid SourceReferenceId,
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
