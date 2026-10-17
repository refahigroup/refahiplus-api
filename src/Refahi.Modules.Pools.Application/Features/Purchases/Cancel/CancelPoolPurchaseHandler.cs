using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Cancel;
public sealed class CancelPoolPurchaseHandler(PoolPurchaseAccess access, IPoolPurchaseRepository repository, IPoolMutationLock gate,
    IMediator mediator) : IRequestHandler<CancelPoolPurchaseCommand, PoolPurchaseDto>
{
    public async Task<PoolPurchaseDto> Handle(CancelPoolPurchaseCommand r, CancellationToken ct)
    {
        var p = await access.Owned(r.Id, r.UserId, ct);
        if (p.OrderId == null)
        {
            await using var lease = await gate.AcquireAsync(r.Id, ct); p = await access.Owned(r.Id, r.UserId, ct);
            if (p.OrderId == null)
            {
                var order = await mediator.Send(new GetOrderByIdempotencyKeyQuery(PoolKeys.Order(p.Id), p.UserId, "Pools"), ct);
                if (order != null) { p.AttachOrder(order.Id, order.OrderNumber); await repository.SaveAsync(ct); }
            }
            if (p.OrderId == null) { p.BeginCancellation(); p.CompleteCancellation(false); await repository.SaveAsync(ct); }
        }
        if (p.OrderId.HasValue) await mediator.Send(new CancelOrderCommand(p.OrderId.Value, "لغو کامل خرید استخر", $"pools-cancel:{p.Id:N}",
            CallerUserId: p.UserId, CallerRole: "User"), ct);
        await mediator.Send(new ReconcilePoolPurchaseCommand(r.Id), ct);
        return access.View(await access.Owned(r.Id, r.UserId, ct));
    }
}
