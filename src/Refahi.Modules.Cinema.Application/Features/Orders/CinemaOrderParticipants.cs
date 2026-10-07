using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Orders.Application.Contracts.Cancellation;
using Refahi.Modules.Orders.Application.Contracts.Payments;
using Refahi.Modules.Cinema.Domain;

namespace Refahi.Modules.Cinema.Application.Features.Orders;
public sealed class CinemaPaymentParticipant(ICinemaMutationLock gate,CinemaOrderService service,ICinemaProviderFactory providers) : IOrderPaymentParticipant
{
    public string SourceModule=>"Cinema";
    public async Task<string?> GetUnavailableReasonAsync(OrderPaymentContext context,CancellationToken ct)
    {
        if(context.SourceReferenceId is not Guid id)return "مرجع رزرو سینما موجود نیست";
        var order=await service.Owned(id,context.UserId,ct);
        return order.OrderId==context.OrderId && order.IsPayable(DateTimeOffset.UtcNow) && order.TotalMinor==context.TotalMinor
            ? null : "رزرو سینما دیگر قابل پرداخت نیست";
    }
    public async Task<IAsyncDisposable> AcquireAsync(OrderPaymentContext context,CancellationToken ct)
    {
        if(context.SourceReferenceId is not Guid id)throw new CinemaException("مرجع سفارش سینما موجود نیست");
        var lease=await gate.AcquireAsync(id,ct);
        try
        {
            var order=await service.Owned(id,context.UserId,ct);
            if(order.OrderId!=context.OrderId||!order.IsPayable(DateTimeOffset.UtcNow)||order.TotalMinor!=context.TotalMinor||order.ProviderOrderId==null)
                throw new CinemaException("رزرو سینما دیگر قابل پرداخت نیست");
            var provider=providers.Get(order.ProviderKey);var known=await provider.GetReservationAsync(order.ProviderOrderId,ct);
            if(!provider.IsReserved(known.Status)||known.TotalMinor!=order.TotalMinor
                ||known.ExpiresAt is not DateTimeOffset expiry||expiry<=DateTimeOffset.UtcNow.AddSeconds(60))
                throw new CinemaException("رزرو تأمین‌کننده معتبر نیست");
            return lease;
        }
        catch{await lease.DisposeAsync();throw;}
    }
}
public sealed class CinemaCancellationParticipant(ICinemaMutationLock gate,CinemaOrderService service) : IOrderCancellationParticipant
{
    public string SourceModule=>"Cinema";
    public async Task<OrderCancellationPreparation> PrepareAsync(OrderCancellationContext context,CancellationToken ct)
    {
        if(context.SourceReferenceId is not Guid id)return new(false,"مرجع سفارش سینما موجود نیست");
        await using var lease=await gate.AcquireAsync(id,ct);
        var order=await service.Owned(id,context.UserId,ct);
        if(order.OrderId!=context.OrderId)return new(false,"مرجع سفارش مالی معتبر نیست");
        if(context.PaymentState=="Paid")order.MarkPaid();
        var complete=await service.CancelProviderAsync(order,ct);
        return new(complete,complete?null:"نتیجه لغو تأمین‌کننده هنوز مشخص نیست");
    }
}
