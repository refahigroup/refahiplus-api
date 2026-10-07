using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;

namespace Refahi.Modules.Cinema.Application.Features.Orders;
public sealed record ReserveCinemaOrderCommand(Guid UserId,string ScheduleId,string[] SeatIds,string IdempotencyKey) : IRequest<CinemaOrderView>;
public sealed record CheckoutCinemaOrderCommand(Guid Id,Guid UserId,long Version,long TotalMinor) : IRequest<CinemaCheckoutResult>;
public sealed record GetCinemaOrderQuery(Guid Id,Guid UserId) : IRequest<CinemaOrderView>;
public sealed record CancelCinemaOrderCommand(Guid Id,Guid UserId) : IRequest<CinemaOrderView>;
public sealed class ReserveCinemaOrderValidator : AbstractValidator<ReserveCinemaOrderCommand>
{
    public ReserveCinemaOrderValidator()
    {
        RuleFor(x=>x.UserId).NotEmpty().WithMessage("شناسه کاربر معتبر نیست");
        RuleFor(x=>x.ScheduleId).Matches("^[0-9A-Za-z]{26}$").WithMessage("شناسه سانس معتبر نیست");
        RuleFor(x=>x.IdempotencyKey).NotEmpty().MaximumLength(128).WithMessage("کلید درخواست معتبر نیست");
        RuleFor(x=>x.SeatIds).NotEmpty().Must(s=>s!=null&&s.Distinct().Count()==s.Length).WithMessage("صندلی تکراری یا خالی مجاز نیست");
        RuleForEach(x=>x.SeatIds).Matches("^[0-9A-Za-z]{26}$").WithMessage("شناسه صندلی معتبر نیست");
    }
}
public sealed class CheckoutCinemaOrderValidator : AbstractValidator<CheckoutCinemaOrderCommand>
{
    public CheckoutCinemaOrderValidator(){RuleFor(x=>x.Id).NotEmpty().WithMessage("شناسه سفارش معتبر نیست");RuleFor(x=>x.UserId).NotEmpty().WithMessage("شناسه کاربر معتبر نیست");RuleFor(x=>x.TotalMinor).GreaterThan(0).WithMessage("مبلغ معتبر نیست");RuleFor(x=>x.Version).GreaterThan(0).WithMessage("نسخه رزرو معتبر نیست");}
}
public sealed class CinemaOrderHandlers(CinemaOrderService service,ILogger<CinemaOrderHandlers> logger) :
    IRequestHandler<ReserveCinemaOrderCommand,CinemaOrderView>,IRequestHandler<CheckoutCinemaOrderCommand,CinemaCheckoutResult>,
    IRequestHandler<GetCinemaOrderQuery,CinemaOrderView>,IRequestHandler<CancelCinemaOrderCommand,CinemaOrderView>,
    INotificationHandler<OrderPaidIntegrationEvent>
{
    public Task<CinemaOrderView> Handle(ReserveCinemaOrderCommand r,CancellationToken ct)=>service.ReserveAsync(r.UserId,r.ScheduleId,r.SeatIds,r.IdempotencyKey,ct);
    public Task<CinemaCheckoutResult> Handle(CheckoutCinemaOrderCommand r,CancellationToken ct)=>service.CheckoutAsync(r.Id,r.UserId,r.Version,r.TotalMinor,ct);
    public Task<CinemaOrderView> Handle(GetCinemaOrderQuery r,CancellationToken ct)=>service.GetAsync(r.Id,r.UserId,ct);
    public Task<CinemaOrderView> Handle(CancelCinemaOrderCommand r,CancellationToken ct)=>service.CancelAsync(r.Id,r.UserId,ct);
    public async Task Handle(OrderPaidIntegrationEvent r,CancellationToken ct)
    {
        if(r.SourceModule!="Cinema"||r.ReferenceType!="CinemaOrder"||!r.SourceReferenceId.HasValue)return;
        using var correlation=logger.BeginScope(new Dictionary<string,object>{{"PaymentId",r.PaymentId},{"OrderId",r.OrderId},{"CinemaOrderId",r.SourceReferenceId.Value}});
        await service.FinalizeAsync(r.SourceReferenceId.Value,ct);
    }
}
public sealed class CinemaOrderCreationValidator(CinemaOrderService service) : INotificationHandler<ValidateOrderSourceNotification>
{
    public async Task Handle(ValidateOrderSourceNotification notification,CancellationToken ct)
    {
        var r=notification.Order;if(!r.SourceModule.Equals("Cinema",StringComparison.OrdinalIgnoreCase))return;
        if(r.ReferenceType!="CinemaOrder"||r.SourceReferenceId is not Guid id)throw new CinemaException("مرجع سفارش سینما معتبر نیست");
        var order=await service.Owned(id,r.UserId,ct);
        if(!order.IsPayable(DateTimeOffset.UtcNow)||order.OrderId.HasValue||r.Items.Count!=1||r.Items[0].Quantity!=1
            ||r.Items[0].UnitPriceMinor!=order.TotalMinor||r.Items[0].DiscountAmountMinor!=0||r.Items[0].CategoryCode!=order.CategoryCode
            ||r.PayableUntil!=order.PayableUntil||r.IdempotencyKey!=$"cinema-order:{id:N}"||r.ShippingFeeMinor!=0
            ||r.DiscountCodeAmountMinor!=0||r.PaymentPostings?.Count>0||r.FinancialSnapshot!=null
            ||r.ShippingAddressId!=null||r.DiscountCode!=null||r.Items[0].SourceItemId!=id||r.SagaId!=id)
            throw new CinemaException("سفارش مالی با رزرو سینما مطابقت ندارد");
    }
}

public sealed class ReconcileCinemaOrderHandler(CinemaOrderService service) : IRequestHandler<ReconcileCinemaOrderCommand>
{
    public async Task<Unit> Handle(ReconcileCinemaOrderCommand request,CancellationToken ct){await service.ReconcileAsync(request.Id,ct);return Unit.Value;}
}

public sealed class GetCinemaOrderValidator : AbstractValidator<GetCinemaOrderQuery>
{public GetCinemaOrderValidator(){RuleFor(x=>x.Id).NotEmpty().WithMessage("شناسه سفارش معتبر نیست");RuleFor(x=>x.UserId).NotEmpty().WithMessage("شناسه کاربر معتبر نیست");}}
public sealed class CancelCinemaOrderValidator : AbstractValidator<CancelCinemaOrderCommand>
{public CancelCinemaOrderValidator(){RuleFor(x=>x.Id).NotEmpty().WithMessage("شناسه سفارش معتبر نیست");RuleFor(x=>x.UserId).NotEmpty().WithMessage("شناسه کاربر معتبر نیست");}}
