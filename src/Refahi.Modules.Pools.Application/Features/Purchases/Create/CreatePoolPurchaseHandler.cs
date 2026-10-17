using Microsoft.Extensions.Logging;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Create;

public sealed class CreatePoolPurchaseHandler(IPoolPurchaseRepository repository, PoolQuoteResolver quotes,
    PoolPurchaseAccess access, IPoolMutationLock gate, IPoolReservationGateway reservation, IOptionsMonitor<PoolsOptions> options,
    TimeProvider clock, ILogger<CreatePoolPurchaseHandler> logger) : IRequestHandler<CreatePoolPurchaseCommand, PoolPurchaseDto>
{
    public async Task<PoolPurchaseDto> Handle(CreatePoolPurchaseCommand r, CancellationToken ct)
    {
        if (!options.CurrentValue.PurchaseEnabled) throw new PoolException("خرید استخر فعال نیست", 503, "PURCHASE_DISABLED");
        var fingerprint = PoolKeys.Hash(JsonSerializer.Serialize(new { Choices = r.Choices.OrderBy(x => x.SessionId), r.ExpectedTotalMinor }));
        await using var lease = await gate.AcquireAsync(PoolKeys.Lock($"purchase:{r.UserId:N}:{r.IdempotencyKey}"), ct);
        var existing = await repository.FindAsync(r.UserId, r.IdempotencyKey, ct);
        if (existing != null)
        {
            if (existing.Fingerprint != fingerprint) throw new PoolException("کلید درخواست با اطلاعات دیگری استفاده شده است", 409, "IDEMPOTENCY_MISMATCH");
            return access.View(existing);
        }
        var requests = await quotes.ResolveAsync(r.Choices, ct); var total = requests.Aggregate(0L, (sum, x) => checked(sum + x.TotalMinor));
        if (r.ExpectedTotalMinor != total) throw new PoolException("قیمت تغییر کرده است؛ مبلغ جدید را تأیید کنید", 409, "PRICE_CHANGED",
            new PoolQuoteDto(requests.Select(x => new PoolQuoteLine(x.SessionId, x.TotalMinor)).ToArray(), total));
        foreach (var request in requests) if (!await reservation.ReserveAsync(request.Id, ct)) throw new PoolException("درخواست پذیرفته نشد");
        var now = clock.GetUtcNow(); var until = now.AddMinutes(options.CurrentValue.ReservationMinutes);
        if (requests.Min(x => x.StartsAtUtc) < until) until = requests.Min(x => x.StartsAtUtc);
        var purchase = PoolPurchase.Create(r.UserId, r.IdempotencyKey, fingerprint, requests, now, until);
        await repository.AddAsync(purchase, ct); logger.LogInformation("Pool purchase created. PurchaseId={PurchaseId} UserId={UserId} AmountMinor={AmountMinor}",purchase.Id,purchase.UserId,purchase.TotalMinor); return access.View(purchase);
    }
}
