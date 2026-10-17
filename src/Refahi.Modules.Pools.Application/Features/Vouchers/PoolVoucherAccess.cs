using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Features.Operators;
using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Application.Features.Vouchers;
public sealed class PoolVoucherAccess(IPoolPurchaseRepository repository, PoolOperatorAccess operators, TimeProvider clock)
{
    public static string Hash(string code) => PoolKeys.Hash(code.Trim().Replace("-", "", StringComparison.Ordinal).ToUpperInvariant());
    public async Task<(PoolPurchase Purchase, PoolRequest Request)> Find(Guid user, Guid pool, string code, CancellationToken ct)
    {
        await operators.Ensure(user, pool, ct);
        var hash = Hash(code); var p = await repository.FindByCodeAsync(hash, ct);
        var r = p?.Requests.SingleOrDefault(x => x.CodeHash == hash && x.PoolId == pool);
        if (p == null || r == null) throw new PoolException("ووچر یافت نشد یا متعلق به این استخر نیست", 404, "VOUCHER_NOT_FOUND");
        return (p, r);
    }
    public PoolVoucherValidationDto View(PoolPurchase p, PoolRequest r) => new(r.Id, r.PoolId, r.PoolName, r.Date, r.Time,
        r.AdultCount, r.ChildCount, r.VoucherStatus == PoolVoucherStatus.Issued && clock.GetUtcNow() >= r.EndsAtUtc ? "Expired" : PoolPublicStates.Voucher(r.VoucherStatus) ?? "None",
        p.Status == PoolPurchaseStatus.Paid && r.IsRedeemable(clock.GetUtcNow()), r.RedeemedAtUtc);
}
