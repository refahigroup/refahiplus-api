using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Commands;

/// <summary>
/// تغییر وضعیت سفارش — توسط تامین‌کننده/ادمین
/// </summary>
public sealed record UpdateOrderStatusCommand(
    Guid OrderId,
    OrderStatusInput NewStatus
) : IRequest<UpdateOrderStatusResponse>;

public enum OrderStatusInput : short
{
    Processing = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6
}

public sealed record UpdateOrderStatusResponse(Guid OrderId, string Status);
