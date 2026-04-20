using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Events;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;

namespace Refahi.Modules.Orders.Application.Features.CancelOrder;

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, CancelOrderResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IMediator _mediator;
    private readonly IPublisher _publisher;

    public CancelOrderCommandHandler(IOrderRepository orderRepository, IMediator mediator, IPublisher publisher)
    {
        _orderRepository = orderRepository;
        _mediator = mediator;
        _publisher = publisher;
    }

    public async Task<CancelOrderResponse> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdWithItemsAsync(request.OrderId, cancellationToken)
            ?? throw new InvalidOperationException("سفارش یافت نشد");

        // Idempotency: اگر قبلاً لغو شده، همان پاسخ را برگردان (safe retry)
        if (order.Status == OrderStatus.Cancelled)
        {
            var alreadyCancelledAction = order.PaymentState switch
            {
                PaymentState.Released => "Released",
                PaymentState.Refunded => "Refunded",
                _ => "NoPayment"
            };
            return new CancelOrderResponse(order.Id, "Cancelled", alreadyCancelledAction);
        }

        var prevPaymentState = order.PaymentState;
        order.Cancel();

        string paymentAction;

        if (prevPaymentState == PaymentState.Reserved && order.PaymentIntentId.HasValue)
        {
            // Release (مبلغ رزرو شده → آزاد می‌شود)
            await _mediator.Send(new ReleasePaymentIntentCommand(
                IntentId: order.PaymentIntentId.Value,
                IdempotencyKey: $"order-release-{request.IdempotencyKey}"),
                cancellationToken);

            order.MarkAsReleased();
            paymentAction = "Released";
        }
        else if (prevPaymentState == PaymentState.Paid && order.PaymentId.HasValue)
        {
            // Refund (پول برمی‌گردد → مطابق allocation اصلی)
            await _mediator.Send(new RefundPaymentCommand(
                PaymentId: order.PaymentId.Value,
                IdempotencyKey: $"order-refund-{request.IdempotencyKey}",
                Reason: request.Reason,
                MetadataJson: null),
                cancellationToken);

            order.MarkAsRefunded();
            paymentAction = "Refunded";
        }
        else
        {
            paymentAction = "NoPayment";
        }

        await _orderRepository.UpdateAsync(order, cancellationToken);
        // Domain events (raised during Cancel/MarkAsReleased/MarkAsRefunded) captured to Outbox via SaveChangesAsync override

        // OrderCancelledEvent is published directly here because it carries paymentAction context that the domain doesn't know
        await _publisher.Publish(new OrderCancelledEvent(
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            UserId: order.UserId,
            PaymentAction: paymentAction,
            OccurredAt: DateTimeOffset.UtcNow), cancellationToken);

        return new CancelOrderResponse(order.Id, "Cancelled", paymentAction);
    }
}
