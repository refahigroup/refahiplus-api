using FluentValidation;
using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Orders.Application.Services;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Promotions.Application.Contracts;
namespace Refahi.Modules.Orders.Application.Features.Promotions;

public sealed class OrderPromotionHandlers(IOrderRepository orders, IOrderMutationLock gate, OrderPromotionService service, ISender sender) :
    IRequestHandler<PreviewOrderPromotionQuery, AppliedOrderPromotionDto>, IRequestHandler<ApplyOrderPromotionCommand, AppliedOrderPromotionDto>,
    IRequestHandler<RemoveOrderPromotionCommand, AppliedOrderPromotionDto>, IRequestHandler<RecoverOrderPromotionCommand>,
    INotificationHandler<OrderPaidIntegrationEvent>, INotificationHandler<OrderCancelledIntegrationEvent>, INotificationHandler<OrderRefundedIntegrationEvent>
{
    public async Task<AppliedOrderPromotionDto> Handle(PreviewOrderPromotionQuery r, CancellationToken ct)
    { await using var held = await gate.AcquireAsync(r.OrderId, ct); return await service.PreviewAsync(await Owned(r.OrderId, r.UserId, ct), r.Code, r.ExpectedPricingVersion, ct); }
    public async Task<AppliedOrderPromotionDto> Handle(ApplyOrderPromotionCommand r, CancellationToken ct)
    { await using var held = await gate.AcquireAsync(r.OrderId, ct); return await service.MutateAsync(await Owned(r.OrderId, r.UserId, ct), r.Code, r.ExpectedPricingVersion, r.IdempotencyKey, ct); }
    public async Task<AppliedOrderPromotionDto> Handle(RemoveOrderPromotionCommand r, CancellationToken ct)
    { await using var held = await gate.AcquireAsync(r.OrderId, ct); return await service.MutateAsync(await Owned(r.OrderId, r.UserId, ct), null, r.ExpectedPricingVersion, r.IdempotencyKey, ct); }
    public async Task<Unit> Handle(RecoverOrderPromotionCommand r, CancellationToken ct)
    { await Recover(r.OrderId, ct, r.ReservationId); return Unit.Value; }
    public Task Handle(OrderPaidIntegrationEvent r, CancellationToken ct) => Recover(r.OrderId, ct);
    public Task Handle(OrderCancelledIntegrationEvent r, CancellationToken ct) => Recover(r.OrderId, ct);
    public Task Handle(OrderRefundedIntegrationEvent r, CancellationToken ct) => Recover(r.OrderId, ct);
    private async Task Recover(Guid id, CancellationToken ct, Guid? reservationId = null)
    {
        await using var held = await gate.AcquireAsync(id, ct);
        var order = await orders.GetByIdWithItemsAsync(id, ct);
        if (order != null) await service.ReconcileAsync(order, ct);
        if (reservationId.HasValue && order?.PromotionReservationId != reservationId)
        {
            var reservation = await sender.Send(new GetPromotionReservationQuery(reservationId.Value), ct);
            if (reservation != null && reservation.Quote.ExpiresAt <= DateTimeOffset.UtcNow && reservation.Status is "Reserved" or "PaymentPending")
            {
                var known = await sender.Send(new Refahi.Modules.Wallets.Application.Contracts.Queries.FindOrderPaymentQuery(id, $"order-reserve-{id:N}"), ct);
                if (known == null || known.Status == 3) await sender.Send(new ReleasePromotionCommand(reservation.Id, id, true), ct);
                else throw new PromotionException("رزرو یتیم دارای عملیات مالی است و نیازمند بررسی است", 409);
            }
        }
    }
    private async Task<Refahi.Modules.Orders.Domain.Aggregates.Order> Owned(Guid id, Guid user, CancellationToken ct)
    { var order = await orders.GetByIdWithItemsAsync(id, ct) ?? throw new PromotionException("سفارش یافت نشد", 404); if (order.UserId != user) throw new PromotionException("دسترسی مجاز نیست", 403); return order; }
}
public sealed class ApplyOrderPromotionValidator : AbstractValidator<ApplyOrderPromotionCommand>
{
    public ApplyOrderPromotionValidator() { RuleFor(x => x.Code).NotEmpty().MaximumLength(50).WithMessage("کد معتبر نیست"); RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200).WithMessage("کلید درخواست معتبر نیست"); RuleFor(x => x.ExpectedPricingVersion).GreaterThan(0).WithMessage("نسخه قیمت معتبر نیست"); }
}
public sealed class RemoveOrderPromotionValidator : AbstractValidator<RemoveOrderPromotionCommand>
{
    public RemoveOrderPromotionValidator() { RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200).WithMessage("کلید درخواست معتبر نیست"); RuleFor(x => x.ExpectedPricingVersion).GreaterThan(0).WithMessage("نسخه قیمت معتبر نیست"); }
}
public sealed class PreviewOrderPromotionValidator : AbstractValidator<PreviewOrderPromotionQuery>
{ public PreviewOrderPromotionValidator() { RuleFor(x => x.Code).NotEmpty().MaximumLength(50).WithMessage("کد معتبر نیست"); RuleFor(x => x.ExpectedPricingVersion).GreaterThan(0).WithMessage("نسخه قیمت معتبر نیست"); } }
