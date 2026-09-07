using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Services;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Features.CancelOrder;

public sealed class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IOrderMutationLock mutationLock,
    OrderCancellationService cancellationService
) : IRequestHandler<CancelOrderCommand, CancelOrderResponse>
{
    public async Task<CancelOrderResponse> Handle(CancelOrderCommand request, CancellationToken ct)
    {
        await using var heldLock = await mutationLock.AcquireAsync(request.OrderId, ct);
        var order =
            await orderRepository.GetByIdWithItemsAsync(request.OrderId, ct)
            ?? throw new InvalidOperationException("سفارش یافت نشد");

        if (!string.Equals(request.CallerRole, "Admin", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.CallerRole, "System", StringComparison.OrdinalIgnoreCase)
            && request.CallerUserId != order.UserId)
            throw new UnauthorizedAccessException("دسترسی به لغو این سفارش مجاز نیست");

        return await cancellationService.CancelAsync(
            order,
            request.Reason,
            request.VoucherRefundOverrideId,
            ct
        );
    }
}
