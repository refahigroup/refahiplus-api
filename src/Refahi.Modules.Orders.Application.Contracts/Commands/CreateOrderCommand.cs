using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Commands;

/// <summary>
/// ایجاد سفارش جدید — توسط ماژول‌های فروش فراخوانی می‌شود
/// </summary>
public sealed record CreateOrderCommand(
    Guid UserId,
    string SourceModule,
    Guid SourceReferenceId,
    List<CreateOrderItemInput> Items,
    string IdempotencyKey
) : IRequest<CreateOrderResponse>;

public sealed record CreateOrderItemInput(
    string Title,
    long UnitPriceMinor,
    int Quantity,
    long DiscountAmountMinor,
    Guid SourceItemId,
    string CategoryCode,
    string[]? Tags,
    string? MetadataJson);

public sealed record CreateOrderResponse(
    Guid OrderId,
    string OrderNumber,
    long FinalAmountMinor);
