using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Cancellation;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Exceptions;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Store.Application.Contracts.Vouchers;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;

namespace Refahi.Modules.Orders.Application.Services;

public sealed class OrderCancellationService(
    IOrderRepository orderRepository,
    IMediator mediator,
    IPublisher publisher,
    IEnumerable<IOrderCancellationParticipant> cancellationParticipants
)
{
    public OrderCancellationService(IOrderRepository orderRepository, IMediator mediator, IPublisher publisher)
        : this(orderRepository, mediator, publisher, Array.Empty<IOrderCancellationParticipant>())
    {
    }

    public Task<CancelOrderResponse> CancelAsync(
        Order order,
        string? reason,
        CancellationToken ct
    ) => CancelAsync(order, reason, voucherRefundOverrideId: null, ct);

    public async Task<CancelOrderResponse> CancelAsync(
        Order order,
        string? reason,
        Guid? voucherRefundOverrideId,
        CancellationToken ct
    )
    {
        if (order.PromotionReservationId.HasValue && order.PaymentState is PaymentState.Unpaid or PaymentState.Reserved)
        {
            var known = await mediator.Send(new Refahi.Modules.Wallets.Application.Contracts.Queries.FindOrderPaymentQuery(order.Id, $"order-reserve-{order.Id:N}"), ct);
            if (known != null && order.PaymentState == PaymentState.Unpaid) order.MarkAsReserved(known.IntentId);
            if (known?.Status == 2 && known.PaymentId.HasValue && order.PaymentState == PaymentState.Reserved) order.MarkAsPaid(known.PaymentId.Value);
        }

        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "لغو سفارش و بازگشت وجه"
            : reason.Trim();

        var participant = cancellationParticipants.FirstOrDefault(x =>
            x.SourceModule.Equals(order.SourceModule, StringComparison.OrdinalIgnoreCase));
        if (participant is not null && order.Status is not OrderStatus.Cancelled and not OrderStatus.Refunded)
        {
            var preparation = await participant.PrepareAsync(new OrderCancellationContext(
                order.Id, order.UserId, order.SourceModule, order.SourceReferenceId,
                order.ReferenceType, order.PaymentState.ToString(), normalizedReason), ct);
            if (!preparation.CanContinue)
                throw new OrderStateConflictException(preparation.Message ?? "لغو سرویس مبدا هنوز تکمیل نشده است");
        }
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Refunded)
        {
            var completedAction = order.PaymentState switch
            {
                PaymentState.Released => "Released",
                PaymentState.Refunded => "Refunded",
                _ => "NoPayment",
            };
            return new CancelOrderResponse(order.Id, "Cancelled", completedAction);
        }

        var paymentAction = "NoPayment";
        if (order.PaymentState == PaymentState.Reserved && order.PaymentIntentId.HasValue)
        {
            var release = await mediator.Send(
                new ReleasePaymentIntentCommand(
                    order.PaymentIntentId.Value,
                    $"order-release-{order.Id:N}"
                ),
                ct
            );
            EnsureCompleted(release.Status, "آزادسازی مبلغ سفارش هنوز تکمیل نشده است");
            order.Cancel();
            order.MarkAsReleased();
            paymentAction = "Released";
        }
        else if (order.PaymentState == PaymentState.Paid && order.PaymentId.HasValue)
        {
            if (
                order.SourceModule.Equals("Store", StringComparison.OrdinalIgnoreCase)
                && order.ReferenceType.Equals("StoreOrder", StringComparison.OrdinalIgnoreCase)
            )
                await mediator.Send(
                    new PrepareStoreOrderRefundCommand(
                        order.Id,
                        normalizedReason,
                        voucherRefundOverrideId
                    ),
                    ct
                );

            var refund = await mediator.Send(
                new RefundPaymentCommand(
                    order.PaymentId.Value,
                    $"order-refund-{order.Id:N}",
                    normalizedReason,
                    MetadataJson: null
                ),
                ct
            );
            EnsureCompleted(refund.Status, "بازگشت وجه سفارش هنوز تکمیل نشده است");
            order.Cancel();
            order.MarkAsRefunded();
            paymentAction = "Refunded";
        }
        else
        {
            order.Cancel();
        }

        order.RecordCancellation(paymentAction);
        await orderRepository.UpdateAsync(order, ct);

        return new CancelOrderResponse(order.Id, "Cancelled", paymentAction);
    }

    private static void EnsureCompleted(CommandStatus status, string message)
    {
        if (status != CommandStatus.Completed)
            throw new InvalidOperationException(message);
    }
}
