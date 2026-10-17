using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Microsoft.Extensions.Options;
using Refahi.Modules.Orders.Application.Contracts.Payments;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Payments;
public sealed class PoolPaymentParticipant(IPoolMutationLock gate, PoolPurchaseAccess access, PoolQuoteResolver quotes,
    IOptionsMonitor<PoolsOptions> options, TimeProvider clock, IMediator mediator) : IOrderPaymentParticipant
{
    public string SourceModule => "Pools";
    public async Task<string?> GetUnavailableReasonAsync(OrderPaymentContext context, CancellationToken ct)
    {
        try
        {
            if (context.SourceReferenceId is not Guid id) return "مرجع خرید موجود نیست";
            var p = await access.Owned(id, context.UserId, ct);
            return options.CurrentValue.PurchaseEnabled && p.OrderId == context.OrderId && p.TotalMinor == context.TotalMinor && p.CanPay(clock.GetUtcNow()) ? null : "خرید قابل پرداخت نیست";
        }
        catch (PoolException ex) { return ex.Message; }
    }
    public async Task<IAsyncDisposable> AcquireAsync(OrderPaymentContext context, CancellationToken ct)
    {
        if (context.SourceReferenceId is not Guid id) throw new PoolException("مرجع خرید موجود نیست");
        var lease = await gate.AcquireAsync(id, ct);
        try
        {
            var p = await access.Owned(id, context.UserId, ct);
            if (!options.CurrentValue.PurchaseEnabled || p.OrderId != context.OrderId || p.TotalMinor != context.TotalMinor || !p.CanPay(clock.GetUtcNow()))
                throw new PoolException("خرید قابل پرداخت نیست", 409, "PURCHASE_UNAVAILABLE");
            var order = await mediator.Send(new GetOrderByIdQuery(context.OrderId, context.UserId, "User"), ct);
            if (!PoolOrderVerification.Matches(p, order)) throw new PoolException("آیتم‌ها و مبلغ سفارش مطابقت ندارند",409,"ORDER_SOURCE_MISMATCH");
            await quotes.ValidateAsync(p, ct);
            if (!p.CanPay(clock.GetUtcNow())) throw new PoolException("مهلت پرداخت پایان یافته است",409,"PURCHASE_EXPIRED");
            return lease;
        }
        catch { await lease.DisposeAsync(); throw; }
    }
}
