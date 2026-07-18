using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Wallets.Application.Contracts.Features.CapturePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreatePaymentIntent;
using System.Linq;

namespace Refahi.Modules.Orders.Application.Features.PayOrder;

public class PayOrderCommandHandler : IRequestHandler<PayOrderCommand, PayOrderResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<PayOrderCommandHandler> _logger;

    public PayOrderCommandHandler(
        IOrderRepository orderRepository,
        IMediator mediator,
        ILogger<PayOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<PayOrderResponse> Handle(PayOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdWithItemsAsync(request.OrderId, cancellationToken)
            ?? throw new InvalidOperationException("سفارش یافت نشد");

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["UserId"] = order.UserId,
            ["SagaId"] = order.SagaId,
            ["HotelRequestId"] = string.Equals(order.ReferenceType, "HotelRequest", StringComparison.OrdinalIgnoreCase)
                ? (Guid?)order.SourceReferenceId
                : null,
            ["OrderId"] = order.Id,
            ["ProviderBookingCode"] = null
        });

        if (!string.Equals(request.CallerRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
            order.UserId != request.CallerUserId)
        {
            throw new UnauthorizedAccessException("دسترسی به پرداخت این سفارش مجاز نیست");
        }

        if (order.PaymentState == PaymentState.Paid && order.PaymentId.HasValue)
        {
            _logger.LogInformation(
                "Order payment replayed as already paid. OrderId={OrderId}, PaymentId={PaymentId}, SagaId={SagaId}",
                order.Id,
                order.PaymentId.Value,
                order.SagaId);
            return new PayOrderResponse(order.Id, order.PaymentId.Value, "Paid");
        }

        await RejectExpiredOrderAsync(order, request.IdempotencyKey, cancellationToken);

        if (order.PaymentState is not PaymentState.Unpaid and not PaymentState.Reserved)
            throw new InvalidOperationException("سفارش در وضعیت قابل پرداخت نیست");

        var allocations = request.Allocations
            .Select(a => new AllocationRequest(a.WalletId, a.AmountMinor))
            .ToList();

        Guid paymentIntentId;
        if (order.PaymentState == PaymentState.Reserved && order.PaymentIntentId.HasValue)
        {
            paymentIntentId = order.PaymentIntentId.Value;
        }
        else
        {
            // Step 1: Reserve via Wallet (CreatePaymentIntent)
            var categoryCodesForIntent = order.Items
                .Select(i => i.CategoryCode)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList();

            var intentResult = await _mediator.Send(new CreatePaymentIntentCommand(
                OrderId: order.Id,
                AmountMinor: order.FinalAmountMinor,
                Currency: order.Currency,
                Allocations: allocations,
                IdempotencyKey: $"order-reserve-{request.IdempotencyKey}",
                OrderItemCategoryCode: categoryCodesForIntent),
                cancellationToken);

            if (intentResult.Data is null)
                throw new InvalidOperationException("ایجاد درخواست پرداخت ناموفق بود");

            paymentIntentId = intentResult.Data.IntentId;
            order.MarkAsReserved(paymentIntentId);

            // Persist reserved state immediately — if Capture fails, PaymentIntentId is saved and Release is possible
            await _orderRepository.UpdateAsync(order, cancellationToken);

            await RejectExpiredOrderAsync(order, request.IdempotencyKey, cancellationToken);

            _logger.LogInformation(
                "Order payment intent reserved. OrderId={OrderId}, PaymentIntentId={PaymentIntentId}, SagaId={SagaId}",
                order.Id,
                paymentIntentId,
                order.SagaId);
        }

        // Step 2: Capture via Wallet (CapturePaymentIntent)
        var captureResult = await _mediator.Send(new CapturePaymentIntentCommand(
            IntentId: paymentIntentId,
            IdempotencyKey: $"order-capture-{request.IdempotencyKey}"),
            cancellationToken);

        if (captureResult.Data is null)
            throw new InvalidOperationException("نهایی‌سازی پرداخت ناموفق بود");

        order.MarkAsPaid(captureResult.Data.PaymentId);

        await _orderRepository.UpdateAsync(order, cancellationToken);
        // Domain events (OrderPaidEvent) are captured to Outbox by SaveChangesAsync override

        _logger.LogInformation(
            "Order payment captured. OrderId={OrderId}, PaymentId={PaymentId}, SagaId={SagaId}",
            order.Id,
            captureResult.Data.PaymentId,
            order.SagaId);

        return new PayOrderResponse(order.Id, captureResult.Data.PaymentId, "Paid");
    }

    private async Task RejectExpiredOrderAsync(Order order, string idempotencyKey, CancellationToken ct)
    {
        if (!order.PayableUntil.HasValue || order.PayableUntil.Value > DateTimeOffset.UtcNow) return;

        await _mediator.Send(new CancelOrderCommand(
            order.Id,
            "مهلت پرداخت سفارش به پایان رسیده است",
            $"expired-{idempotencyKey}"), ct);

        throw new InvalidOperationException("مهلت پرداخت سفارش به پایان رسیده است");
    }
}
