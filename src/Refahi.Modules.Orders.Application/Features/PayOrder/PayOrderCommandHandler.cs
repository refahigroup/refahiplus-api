using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
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

    public PayOrderCommandHandler(IOrderRepository orderRepository, IMediator mediator)
    {
        _orderRepository = orderRepository;
        _mediator = mediator;
    }

    public async Task<PayOrderResponse> Handle(PayOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new InvalidOperationException("سفارش یافت نشد");

        if (order.PaymentState != PaymentState.Unpaid)
            throw new InvalidOperationException("سفارش قبلاً پرداخت شده یا در حال پرداخت است");

        var allocations = request.Allocations
            .Select(a => new AllocationRequest(a.WalletId, a.AmountMinor))
            .ToList();

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

        order.MarkAsReserved(intentResult.Data.IntentId);

        // Persist reserved state immediately — if Capture fails, PaymentIntentId is saved and Release is possible
        await _orderRepository.UpdateAsync(order, cancellationToken);

        // Step 2: Capture via Wallet (CapturePaymentIntent)
        var captureResult = await _mediator.Send(new CapturePaymentIntentCommand(
            IntentId: intentResult.Data.IntentId,
            IdempotencyKey: $"order-capture-{request.IdempotencyKey}"),
            cancellationToken);

        if (captureResult.Data is null)
            throw new InvalidOperationException("نهایی‌سازی پرداخت ناموفق بود");

        order.MarkAsPaid(captureResult.Data.PaymentId);

        await _orderRepository.UpdateAsync(order, cancellationToken);
        // Domain events (OrderPaidEvent) are captured to Outbox by SaveChangesAsync override

        return new PayOrderResponse(order.Id, captureResult.Data.PaymentId, "Paid");
    }
}
