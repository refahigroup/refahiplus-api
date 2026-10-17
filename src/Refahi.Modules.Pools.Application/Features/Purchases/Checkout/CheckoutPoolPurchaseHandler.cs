using Microsoft.Extensions.Logging;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Options;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Checkout;
public sealed class CheckoutPoolPurchaseHandler(PoolPurchaseAccess access, IPoolPurchaseRepository repository, IPoolMutationLock gate,
    IMediator mediator, IOptionsMonitor<PoolsOptions> options, TimeProvider clock, ILogger<CheckoutPoolPurchaseHandler> logger) : IRequestHandler<CheckoutPoolPurchaseCommand, PoolPurchaseCheckoutDto>
{
    public async Task<PoolPurchaseCheckoutDto> Handle(CheckoutPoolPurchaseCommand r, CancellationToken ct)
    {
        if (!options.CurrentValue.PurchaseEnabled) throw new PoolException("خرید استخر فعال نیست", 503);
        await using var lease = await gate.AcquireAsync(r.Id, ct); var p = await access.Owned(r.Id, r.UserId, ct);
        if (p.OrderId.HasValue) return new(p.Id, p.OrderId.Value, p.OrderNumber!, p.TotalMinor);
        if (!p.CanPay(clock.GetUtcNow()) || p.Version != r.Version || p.TotalMinor != r.TotalMinor)
            throw new PoolException("خرید یا مبلغ تغییر کرده است", 409, "PURCHASE_CHANGED");
        var items = p.Requests.Select(x => new CreateOrderItemInput($"{x.PoolName} - {x.Date} - {x.Time}", x.TotalMinor, 1, 0, x.Id,
            PoolKeys.Category, ["pools"], JsonSerializer.Serialize(new { purchaseId = p.Id, requestId = x.Id, x.AdultCount, x.ChildCount }))).ToList();
        var created = await mediator.Send(new CreateOrderCommand(p.UserId, "Pools", p.Id, items, PoolKeys.Order(p.Id),
            ReferenceType: "PoolPurchase", SagaId: p.Id, PayableUntil: p.PayableUntil), ct);
        p.AttachOrder(created.OrderId, created.OrderNumber); await repository.SaveAsync(ct); logger.LogInformation("Pool order attached. PurchaseId={PurchaseId} OrderId={OrderId}",p.Id,p.OrderId);
        return new(p.Id, created.OrderId, created.OrderNumber, created.FinalAmountMinor);
    }
}
