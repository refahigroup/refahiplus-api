using Microsoft.Extensions.Logging;
using MediatR;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Features.Operators;
using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Application.Features.Vouchers.Redeem;
public sealed class RedeemPoolVoucherHandler(PoolVoucherAccess access, PoolOperatorAccess operators, IPoolPurchaseRepository repository,
    IPoolMutationLock gate, TimeProvider clock, ILogger<RedeemPoolVoucherHandler> logger) : IRequestHandler<RedeemPoolVoucherCommand, PoolVoucherValidationDto>
{
    public async Task<PoolVoucherValidationDto> Handle(RedeemPoolVoucherCommand r, CancellationToken ct)
    {
        await operators.Ensure(r.OperatorId, r.PoolId, ct);
        var fingerprint = PoolKeys.Hash($"{r.PoolId:N}:{PoolVoucherAccess.Hash(r.Code)}");
        await using var attempt = await gate.AcquireAsync(PoolKeys.Lock($"redeem:{r.OperatorId:N}:{r.IdempotencyKey}"), ct);
        var previous = await repository.FindRedemptionAsync(r.OperatorId, r.IdempotencyKey, ct);
        if (previous != null)
        {
            if (previous.RedemptionFingerprint != fingerprint) throw new PoolException("کلید درخواست با ووچر دیگری استفاده شده است", 409, "IDEMPOTENCY_MISMATCH");
            var purchase = await repository.GetAsync(previous.PurchaseId, ct) ?? throw new PoolException("خرید یافت نشد", 404);
            return access.View(purchase, purchase.Requests.Single(x => x.Id == previous.Id));
        }
        var found = await access.Find(r.OperatorId, r.PoolId, r.Code, ct);
        await using var lease = await gate.AcquireAsync(found.Purchase.Id, ct);
        var p = await repository.GetAsync(found.Purchase.Id, ct) ?? throw new PoolException("خرید یافت نشد", 404);
        await operators.Ensure(r.OperatorId, r.PoolId, ct);
        var request = p.Requests.Single(x => x.Id == found.Request.Id);
        if (!access.View(p, request).CanRedeem) throw new PoolException("ووچر در این زمان قابل مصرف نیست یا قبلاً مصرف شده است", 409, "VOUCHER_NOT_REDEEMABLE");
        p.Redeem(request.Id, r.OperatorId, r.IdempotencyKey, fingerprint, clock.GetUtcNow()); await repository.SaveAsync(ct); logger.LogInformation("Pool voucher consumed. PurchaseId={PurchaseId} RequestId={RequestId} PoolId={PoolId} OperatorId={OperatorId}",p.Id,request.Id,r.PoolId,r.OperatorId);
        return access.View(p, request);
    }
}
