using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Events;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;

namespace Refahi.Modules.Orders.Application.Services;

public sealed class OrderCancellationService(
    IOrderRepository orderRepository,
    IMediator mediator,
    IPublisher publisher)
{
    public async Task<CancelOrderResponse> CancelAsync(Order order, string reason, CancellationToken ct)
    {
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Refunded)
        {
            var completedAction = order.PaymentState switch
            {
                PaymentState.Released => "Released",
                PaymentState.Refunded => "Refunded",
                _ => "NoPayment"
            };
            return new CancelOrderResponse(order.Id, "Cancelled", completedAction);
        }

        var paymentAction = "NoPayment";
        if (order.PaymentState == PaymentState.Reserved && order.PaymentIntentId.HasValue)
        {
            var release = await mediator.Send(new ReleasePaymentIntentCommand(
                order.PaymentIntentId.Value,
                $"order-release-{order.Id:N}"), ct);
            EnsureCompleted(release.Status, "آزادسازی مبلغ سفارش هنوز تکمیل نشده است");
            order.Cancel();
            order.MarkAsReleased();
            paymentAction = "Released";
        }
        else if (order.PaymentState == PaymentState.Paid && order.PaymentId.HasValue)
        {
            var refund = await mediator.Send(new RefundPaymentCommand(
                order.PaymentId.Value,
                $"order-refund-{order.Id:N}",
                reason,
                MetadataJson: null), ct);
            EnsureCompleted(refund.Status, "بازگشت وجه سفارش هنوز تکمیل نشده است");
            order.Cancel();
            order.MarkAsRefunded();
            paymentAction = "Refunded";
        }
        else
        {
            order.Cancel();
        }

        await orderRepository.UpdateAsync(order, ct);

        await publisher.Publish(new OrderCancelledEvent(
            order.Id, order.OrderNumber, order.UserId, paymentAction, DateTimeOffset.UtcNow), ct);

        if (paymentAction == "Refunded")
            await publisher.Publish(new OrderRefundedIntegrationEvent(
                order.Id, order.OrderNumber, order.SourceOwnerId, order.SourceShopId,
                order.FinalAmountMinor, DateTimeOffset.UtcNow), ct);

        return new CancelOrderResponse(order.Id, "Cancelled", paymentAction);
    }

    private static void EnsureCompleted(CommandStatus status, string message)
    {
        if (status != CommandStatus.Completed)
            throw new InvalidOperationException(message);
    }
}
