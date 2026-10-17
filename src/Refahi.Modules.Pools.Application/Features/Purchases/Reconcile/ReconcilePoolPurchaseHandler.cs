using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Reconcile;
public sealed class ReconcilePoolPurchaseHandler(IPoolPurchaseRepository repository, IPoolMutationLock gate, IMediator mediator,
    TimeProvider clock) : IRequestHandler<ReconcilePoolPurchaseCommand>
{
    public async Task<Unit> Handle(ReconcilePoolPurchaseCommand r, CancellationToken ct)
    {
        PoolPurchase? p;
        await using (var lease = await gate.AcquireAsync(r.Id, ct))
        {
            p = await repository.GetAsync(r.Id, ct); if (p == null || p.Status is PoolPurchaseStatus.Cancelled or PoolPurchaseStatus.Refunded) return Unit.Value;
            p.ExpireVouchers(clock.GetUtcNow());
            var order = p.OrderId.HasValue ? await mediator.Send(new GetOrderByIdQuery(p.OrderId.Value, p.UserId, "User"), ct)
                : await mediator.Send(new GetOrderByIdempotencyKeyQuery(PoolKeys.Order(p.Id), p.UserId, "Pools"), ct);
            if (order != null && !p.OrderId.HasValue) p.AttachOrder(order.Id, order.OrderNumber);
            if (order?.Status is "Cancelled" or "Refunded") { p.BeginCancellation(); p.CompleteCancellation(order.PaymentState == "Refunded"); }
            if (order == null && !p.CanPay(clock.GetUtcNow())) { p.BeginCancellation(); p.CompleteCancellation(false); }
            await repository.SaveAsync(ct);
        }
        // Never acquire the financial Orders lock while holding the Pools source lock.
        if (p.OrderId.HasValue)
        {
            if (p.Status == PoolPurchaseStatus.CancellationPending || p.Status == PoolPurchaseStatus.AwaitingPayment && p.PayableUntil <= clock.GetUtcNow())
            {
                var order = await mediator.Send(new GetOrderByIdQuery(p.OrderId.Value, p.UserId, "User"), ct);
                if (order?.PaymentState != "Paid" || p.Status == PoolPurchaseStatus.CancellationPending)
                    await mediator.Send(new CancelOrderCommand(p.OrderId.Value, "بازیابی یا انقضای خرید استخر", $"pools-cancel:{p.Id:N}", CallerUserId: p.UserId, CallerRole: "User"), ct);
                else await mediator.Send(new IssuePoolVouchersCommand(p.Id), ct);
            }
            else await mediator.Send(new IssuePoolVouchersCommand(p.Id), ct);
        }
        return Unit.Value;
    }
}
