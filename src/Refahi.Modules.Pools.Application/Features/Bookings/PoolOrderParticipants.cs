using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Orders.Application.Contracts.Payments;
using Refahi.Modules.Orders.Application.Contracts.Cancellation;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Application.Features.Bookings;

public sealed class PoolPaymentParticipant(IPoolMutationLock gate, PoolBookingService service,
    IPoolProviderFactory providers, IPoolPurchasePolicy policy) : IOrderPaymentParticipant
{
    public string SourceModule => "Pools";
    public async Task<string?> GetUnavailableReasonAsync(OrderPaymentContext context, CancellationToken ct)
    {
        try
        {
            if (context.SourceReferenceId is not Guid id) return "مرجع خرید استخر موجود نیست";
            var b = await service.Owned(id, context.UserId, ct); policy.EnsureAllowed(b.IsMock);
            return b.OrderId == context.OrderId && b.TotalMinor == context.TotalMinor && b.IsPayable(DateTimeOffset.UtcNow)
                ? null : "رزرو دیگر قابل پرداخت نیست";
        }
        catch (PoolException ex) { return ex.Message; }
    }
    public async Task<IAsyncDisposable> AcquireAsync(OrderPaymentContext context, CancellationToken ct)
    {
        if (context.SourceReferenceId is not Guid id) throw new PoolException("مرجع خرید استخر موجود نیست");
        var lease = await gate.AcquireAsync(id, ct);
        try
        {
            var b = await service.Owned(id, context.UserId, ct); policy.EnsureAllowed(b.IsMock);
            if (b.OrderId != context.OrderId || b.TotalMinor != context.TotalMinor || !b.IsPayable(DateTimeOffset.UtcNow)
                || b.ProviderReference == null) throw new PoolException("رزرو دیگر قابل پرداخت نیست");
            var provider = providers.Get(b.ProviderKey);
            if (!provider.Capabilities.Status || provider is not IPoolStatusProvider status) throw new PoolException("استعلام رزرو ممکن نیست");
            var known = await status.StatusAsync(b.ProviderReference, ct);
            if (known?.Status != "Reserved" || known.TotalMinor != b.TotalMinor || known.IsMock != b.IsMock
                || known.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(60)) throw new PoolException("رزرو تأمین‌کننده معتبر نیست");
            return lease;
        }
        catch { await lease.DisposeAsync(); throw; }
    }
}
public sealed class PoolCancellationParticipant(IPoolMutationLock gate, PoolBookingService service) : IOrderCancellationParticipant
{
    public string SourceModule => "Pools";
    public async Task<OrderCancellationPreparation> PrepareAsync(OrderCancellationContext context, CancellationToken ct)
    {
        if (context.SourceReferenceId is not Guid id) return new(false, "مرجع خرید استخر موجود نیست");
        await using var lease = await gate.AcquireAsync(id, ct);
        var b = await service.Owned(id, context.UserId, ct);
        if (b.OrderId != context.OrderId || context.ReferenceType != "PoolBooking") return new(false, "مرجع سفارش مالی معتبر نیست");
        if (context.PaymentState == "Paid" && b.PaymentStatus == "Unpaid") b.MarkPaid();
        var completed = await service.CancelProviderAsync(b, ct);
        return new(completed, completed ? null : "لغو تأمین‌کننده هنوز تأیید نشده است");
    }
}
public sealed class PoolOrderSourceValidator(PoolBookingService service, IPoolPurchasePolicy policy) : INotificationHandler<ValidateOrderSourceNotification>
{
    public async Task Handle(ValidateOrderSourceNotification notification, CancellationToken ct)
    {
        var r = notification.Order;
        if (!r.SourceModule.Equals("Pools", StringComparison.OrdinalIgnoreCase) && r.ReferenceType != "PoolBooking") return;
        if (r.SourceModule != "Pools" || r.ReferenceType != "PoolBooking" || r.SourceReferenceId is not Guid id) throw new PoolException("مرجع سفارش استخر معتبر نیست");
        var b = await service.Owned(id, r.UserId, ct); policy.EnsureAllowed(b.IsMock);
        if (!b.IsPayable(DateTimeOffset.UtcNow) || b.OrderId.HasValue || r.Items.Count != 1 || r.Items[0].Quantity != 1
            || r.Items[0].UnitPriceMinor != b.TotalMinor || r.Items[0].DiscountAmountMinor != 0 || r.Items[0].SourceItemId != id
            || r.Items[0].CategoryCode != "entertainment.waterpark" || r.PayableUntil != b.PayableUntil || r.SagaId != id
            || r.IdempotencyKey != PoolBookingService.OrderKey(id) || r.ShippingFeeMinor != 0 || r.DiscountCodeAmountMinor != 0
            || r.ShippingAddressId != null || r.ShippingAddressSnapshotJson != null || r.DiscountCode != null
            || r.FinancialSnapshot != null || r.PaymentPostings?.Count > 0 || r.SourceOwnerId != null || r.SourceShopId != null)
            throw new PoolException("سفارش مالی با رزرو استخر مطابقت ندارد");
    }
}
public sealed class PoolBookingHandlers(PoolBookingService service) :
    IRequestHandler<ReservePoolBookingCommand, PoolBookingView>, IRequestHandler<CheckoutPoolBookingCommand, PoolCheckout>,
    IRequestHandler<GetPoolBookingQuery, PoolBookingView>, IRequestHandler<CancelPoolBookingCommand, PoolBookingView>,
    IRequestHandler<ReconcilePoolBookingCommand>, INotificationHandler<OrderPaidIntegrationEvent>, INotificationHandler<OrderCancelledIntegrationEvent>
{
    public Task<PoolBookingView> Handle(ReservePoolBookingCommand r, CancellationToken ct) => service.ReserveAsync(r, ct);
    public Task<PoolCheckout> Handle(CheckoutPoolBookingCommand r, CancellationToken ct) => service.CheckoutAsync(r, ct);
    public Task<PoolBookingView> Handle(GetPoolBookingQuery r, CancellationToken ct) => service.GetAsync(r.Id, r.UserId, ct);
    public Task<PoolBookingView> Handle(CancelPoolBookingCommand r, CancellationToken ct) => service.CancelAsync(r.Id, r.UserId, ct);
    public async Task<Unit> Handle(ReconcilePoolBookingCommand r, CancellationToken ct) { await service.ReconcileAsync(r.Id, ct); return Unit.Value; }
    public Task Handle(OrderPaidIntegrationEvent e, CancellationToken ct) => e.SourceModule == "Pools" && e.ReferenceType == "PoolBooking"
        && e.SourceReferenceId.HasValue ? service.FinalizeAsync(e.SourceReferenceId.Value, ct) : Task.CompletedTask;
    public async Task Handle(OrderCancelledIntegrationEvent e, CancellationToken ct)
    {
        // This notification runs while Orders holds its cancellation lock; only synchronize the committed payment state.
        if (e.SourceModule == "Pools" && e.ReferenceType == "PoolBooking" && e.SourceReferenceId.HasValue)
            await service.GetAsync(e.SourceReferenceId.Value, e.UserId, ct);
    }
}
