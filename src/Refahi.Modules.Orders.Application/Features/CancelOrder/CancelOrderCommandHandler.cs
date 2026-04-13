using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;

namespace Refahi.Modules.Orders.Application.Features.CancelOrder;

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, CancelOrderResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IMediator _mediator;

    public CancelOrderCommandHandler(IOrderRepository orderRepository, IMediator mediator)
    {
        _orderRepository = orderRepository;
        _mediator = mediator;
    }

    public async Task<CancelOrderResponse> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdWithItemsAsync(request.OrderId, cancellationToken)
            ?? throw new InvalidOperationException("سفارش یافت نشد");

        order.Cancel();

        string paymentAction;

        if (order.PaymentState == PaymentState.Reserved && order.PaymentIntentId.HasValue)
        {
            // Release (مبلغ رزرو شده → آزاد می‌شود)
            await _mediator.Send(new ReleasePaymentIntentCommand(
                IntentId: order.PaymentIntentId.Value,
                IdempotencyKey: $"order-release-{request.IdempotencyKey}"),
                cancellationToken);

            order.MarkAsReleased();
            paymentAction = "Released";
        }
        else if (order.PaymentState == PaymentState.Paid && order.PaymentId.HasValue)
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

        return new CancelOrderResponse(order.Id, "Cancelled", paymentAction);
    }
}
