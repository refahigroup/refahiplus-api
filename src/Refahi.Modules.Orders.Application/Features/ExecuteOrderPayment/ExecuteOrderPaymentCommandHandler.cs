using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;

namespace Refahi.Modules.Orders.Application.Features.ExecuteOrderPayment;

public sealed class ExecuteOrderPaymentCommandHandler(IMediator mediator)
    : IRequestHandler<ExecuteOrderPaymentCommand, ExecuteOrderPaymentResponse>
{
    public async Task<ExecuteOrderPaymentResponse> Handle(
        ExecuteOrderPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var options = await mediator.Send(
            new GetOrderPaymentOptionsQuery(request.OrderId, request.UserId, "User"),
            cancellationToken);
        if (options is null)
            throw new InvalidOperationException("سفارش یافت نشد");

        if (!options.IsCovered || options.DeficitMinor != 0)
            return new(request.OrderId, "InsufficientFunds", null, null, "insufficient_funds");

        var allocations = options.Allocations
            .Select(x => new WalletAllocationInput(x.WalletId, x.AmountMinor))
            .ToList();
        if (allocations.Count == 0 || allocations.Sum(x => x.AmountMinor) != options.FinalAmountMinor)
            throw new InvalidOperationException("تخصیص خودکار پرداخت معتبر نیست");

        try
        {
            var paid = await mediator.Send(
                new PayOrderCommand(
                    request.OrderId,
                    request.UserId,
                    "User",
                    allocations,
                    request.IdempotencyKey,
                    request.DestinationWalletId),
                cancellationToken);
            var status = await mediator.Send(
                new GetOrderPaymentExecutionStatusQuery(request.OrderId, request.UserId),
                cancellationToken);
            return new(paid.OrderId, "Succeeded", paid.PaymentId, status?.PaidAt);
        }
        catch (InsufficientFundsException)
        {
            // Reserve did not commit; the same logical Order payment may be retried
            // after a later top-up with the same financial identity.
            return new(request.OrderId, "InsufficientFunds", null, null, "insufficient_funds");
        }
    }
}
