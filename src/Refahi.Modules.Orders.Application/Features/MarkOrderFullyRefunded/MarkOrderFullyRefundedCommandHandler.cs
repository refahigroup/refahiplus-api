using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Features.MarkOrderFullyRefunded;

public sealed class MarkOrderFullyRefundedCommandHandler(IOrderRepository orders,IOrderMutationLock mutationLock)
    : IRequestHandler<MarkOrderFullyRefundedCommand,MarkOrderFullyRefundedResponse>
{
    public async Task<MarkOrderFullyRefundedResponse> Handle(MarkOrderFullyRefundedCommand request,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(request.SourceReference)||request.SourceReference.Length>128)
            throw new InvalidOperationException("مرجع بازگشت وجه نامعتبر است");
        await using var held=await mutationLock.AcquireAsync(request.OrderId,ct);
        var order=await orders.GetByIdWithItemsAsync(request.OrderId,ct)
            ??throw new InvalidOperationException("سفارش یافت نشد");
        if(order.PaymentState==PaymentState.Refunded)return new(order.Id,"AlreadyRefunded");
        if(order.PaymentState!=PaymentState.Paid||order.PaymentId!=request.WalletPaymentId)
            throw new InvalidOperationException("وضعیت مالی سفارش با بازگشت وجه کامل سازگار نیست");
        order.MarkAsRefunded();await orders.UpdateAsync(order,ct);return new(order.Id,"Refunded");
    }
}
