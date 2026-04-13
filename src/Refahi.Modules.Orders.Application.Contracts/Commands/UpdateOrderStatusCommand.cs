using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Commands;

/// <summary>
/// تغییر وضعیت سفارش — توسط تامین‌کننده/ادمین
/// </summary>
public sealed record UpdateOrderStatusCommand(
    Guid OrderId,
    short NewStatus
) : IRequest<UpdateOrderStatusResponse>;

public sealed record UpdateOrderStatusResponse(Guid OrderId, string Status);
