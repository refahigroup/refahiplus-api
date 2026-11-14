using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Issue;
public sealed class IssuePoolVouchersHandler(IPoolPurchaseRepository repository, IPoolMutationLock gate,
    IPoolCodeProtector protector, IMediator mediator, TimeProvider clock, ILogger<IssuePoolVouchersHandler> logger, IPoolFulfillment? fulfillment = null) : IRequestHandler<IssuePoolVouchersCommand>
{
    public async Task<Unit> Handle(IssuePoolVouchersCommand r, CancellationToken ct)
    {
        var p = await repository.GetAsync(r.Id, ct);
        if (p?.OrderId is not Guid orderId) return Unit.Value;
        var order = await mediator.Send(new GetOrderByIdQuery(orderId, p.UserId, "User"), ct);
        if (!PoolOrderVerification.Matches(p, order) || order!.PaymentState != "Paid" || order.Status == "Cancelled") return Unit.Value;
        await using var lease = await gate.AcquireAsync(r.Id, ct);
        p = await repository.GetAsync(r.Id, ct);
        if (p == null || p.OrderId != orderId || p.Status is not (PoolPurchaseStatus.AwaitingPayment or PoolPurchaseStatus.Paid)) return Unit.Value;
        if (p.Requests.Any(x => x.ReservationStatus != PoolReservationStatus.ExternalReservationBypassed))
        {
            if (fulfillment == null) throw new PoolException("سرویس رزرو آماده نیست", 503);
            p.RecordExternalPayment(); await repository.SaveAsync(ct);
            foreach (var request in p.Requests.Where(x => x.ReservationStatus is PoolReservationStatus.Pending or PoolReservationStatus.Unknown))
            {
                var result = await fulfillment.ReserveAsync(request.Id, new(request.ProviderKey, request.ExternalPoolId,
                    request.ExternalRowId, request.AdultCount, request.ChildCount), ct, request.TotalMinor);
                var status = result.Outcome switch { PoolFulfillmentOutcome.Reserved => PoolReservationStatus.Reserved,
                    PoolFulfillmentOutcome.Rejected => PoolReservationStatus.Rejected, _ => PoolReservationStatus.Unknown };
                p.CompleteExternal(request.Id, status,
                    result.Voucher == null ? null : protector.Protect(result.Voucher), clock.GetUtcNow());
                await repository.SaveAsync(ct);
            }
            if (p.Requests.All(x => x.ReservationStatus == PoolReservationStatus.Rejected))
            { p.BeginCancellation(); await repository.SaveAsync(ct); }
            return Unit.Value;
        }
        if (p.Status != PoolPurchaseStatus.AwaitingPayment) return Unit.Value;
        var codes = p.Requests.ToDictionary(x => x.Id, _ =>
        {
            var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)); return (PoolKeys.Hash(code), protector.Protect(code));
        });
        p.CommitPaid(codes, clock.GetUtcNow()); await repository.SaveAsync(ct); logger.LogInformation("Pool vouchers issued. PurchaseId={PurchaseId} OrderId={OrderId} Count={Count}",p.Id,p.OrderId,p.Requests.Count); return Unit.Value;
    }
}
