using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Features.GetOrderPaymentExecutionStatus;

public sealed class GetOrderPaymentExecutionStatusQueryHandler(IOrderRepository orders)
    : IRequestHandler<GetOrderPaymentExecutionStatusQuery, OrderPaymentExecutionStatusDto?>
{
    public async Task<OrderPaymentExecutionStatusDto?> Handle(
        GetOrderPaymentExecutionStatusQuery request,
        CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdWithItemsAsync(request.OrderId, cancellationToken);
        if (order is null || order.UserId != request.UserId)
            return null;

        var category = order.Items.Count == 1 ? order.Items[0].CategoryCode : string.Empty;
        var status = order.PaymentState switch
        {
            PaymentState.Unpaid => "NotStarted",
            PaymentState.Reserved => "InProgress",
            PaymentState.Paid => "Succeeded",
            PaymentState.Released => "NoCapture",
            PaymentState.Refunded => "Succeeded",
            _ => "Unknown"
        };
        return new(
            order.Id,
            order.UserId,
            order.FinalAmountMinor,
            order.Currency,
            order.SourceModule,
            order.SourceReferenceId,
            order.ReferenceType,
            category,
            status,
            order.PaymentIntentId,
            order.PaymentId,
            order.PaymentState is PaymentState.Paid or PaymentState.Refunded
                ? order.UpdatedAt
                : null);
    }
}
