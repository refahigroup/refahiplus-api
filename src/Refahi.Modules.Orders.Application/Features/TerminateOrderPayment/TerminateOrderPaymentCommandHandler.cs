using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Usecases;

namespace Refahi.Modules.Orders.Application.Features.TerminateOrderPayment;

public sealed class TerminateOrderPaymentCommandHandler(
    IOrderRepository orders,
    IOrderMutationLock mutationLock,
    IWalletPaymentExecutor wallet)
    : IRequestHandler<TerminateOrderPaymentCommand, TerminateOrderPaymentResponse>
{
    public async Task<TerminateOrderPaymentResponse> Handle(
        TerminateOrderPaymentCommand request, CancellationToken ct)
    {
        await using var heldLock = await mutationLock.AcquireAsync(request.OrderId, ct);
        var order = await orders.GetByIdWithItemsAsync(request.OrderId, ct)
            ?? throw new InvalidOperationException("سفارش یافت نشد");
        if (order.UserId != request.UserId)
            throw new UnauthorizedAccessException("دسترسی به پرداخت سفارش مجاز نیست");

        if (order.PaymentState is PaymentState.Paid or PaymentState.Refunded)
            return new(order.Id, "Captured", order.PaymentIntentId, order.PaymentId);
        if (order.PaymentState == PaymentState.Released)
            return new(order.Id, "AlreadyReleased", order.PaymentIntentId);
        if (order.PaymentState == PaymentState.Unpaid)
        {
            if (order.Status != OrderStatus.Cancelled)
                order.Cancel();
            await orders.UpdateAsync(order, ct);
            return new(order.Id, "NoFinancialEffect");
        }
        if (order.PaymentState != PaymentState.Reserved || order.PaymentIntentId is null)
            return new(order.Id, "Unknown", order.PaymentIntentId, order.PaymentId);

        var result = await wallet.ReleaseAsync(new ReleasePaymentIntentCommand(
            order.PaymentIntentId.Value, request.ReleaseReference), ct);
        if (result.Status != CommandStatus.Completed || result.Data is null)
            return new(order.Id, "InProgress", order.PaymentIntentId);

        if (order.Status != OrderStatus.Cancelled)
            order.Cancel();
        order.MarkAsReleased();
        await orders.UpdateAsync(order, ct);
        return new(order.Id, "Released", order.PaymentIntentId);
    }
}
