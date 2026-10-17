using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;

namespace Refahi.Modules.Pools.Application.Features.Purchases;
public sealed class PoolPurchaseAccess(IPoolPurchaseRepository repository, IPoolCodeProtector protector, TimeProvider clock)
{
    public async Task<PoolPurchase> Owned(Guid id, Guid user, CancellationToken ct)
    {
        var purchase = await repository.GetAsync(id, ct) ?? throw new PoolException("خرید یافت نشد", 404, "PURCHASE_NOT_FOUND");
        if (purchase.UserId != user) throw new PoolException("دسترسی به این خرید مجاز نیست", 403, "PURCHASE_FORBIDDEN");
        return purchase;
    }
    public PoolPurchaseDto View(PoolPurchase p)
    {
        var now = clock.GetUtcNow();
        return new(p.Id, p.OrderId, p.OrderNumber, p.Version, p.TotalMinor, PoolPublicStates.Purchase(p.Status), p.PayableUntil, p.CanPay(now),
            (p.Status is PoolPurchaseStatus.AwaitingPayment or PoolPurchaseStatus.Paid) && !p.Requests.Any(r => r.VoucherStatus == PoolVoucherStatus.Redeemed),
            p.Requests.Select(r => new PoolVoucherDto(r.Id, r.SessionId, r.PoolId, r.PoolName, r.Date, r.Time, r.AdultCount, r.ChildCount,
                r.TotalMinor, r.StartsAtUtc, r.EndsAtUtc, "ExternalReservationBypassed",
                r.VoucherStatus == PoolVoucherStatus.Issued && now >= r.EndsAtUtc ? "Expired" : PoolPublicStates.Voucher(r.VoucherStatus),
                p.Status == PoolPurchaseStatus.Paid && (r.VoucherStatus is PoolVoucherStatus.Issued or PoolVoucherStatus.Redeemed or PoolVoucherStatus.Expired) && r.CodeCiphertext != null
                    ? protector.Unprotect(r.CodeCiphertext) : null, r.RedeemedAtUtc)).ToArray());
    }
}
