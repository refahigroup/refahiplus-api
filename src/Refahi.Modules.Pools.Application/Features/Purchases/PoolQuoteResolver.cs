using MediatR;
using Refahi.Modules.Identity.Application.Contracts.AuthorizationGrants;
using Refahi.Modules.Pools.Application.Features.Operators;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;

namespace Refahi.Modules.Pools.Application.Features.Purchases;
public sealed class PoolQuoteResolver(IPoolCatalogRepository catalog, IPoolProviderFactory providers,
    IPoolSessionTimeParser times, IOptionsMonitor<PoolsOptions> options, TimeProvider clock, IMediator mediator)
{
    public async Task<IReadOnlyList<PoolRequest>> ResolveAsync(IReadOnlyList<PoolChoice> choices, CancellationToken ct)
    {
        if (!options.CurrentValue.CatalogEnabled) throw new PoolException("سرویس استخر فعال نیست", 503, "CATALOG_DISABLED");
        var result = new List<PoolRequest>();
        var grants = options.CurrentValue.PurchaseEnabled
            ? await mediator.Send(new GetAuthorizationGrantsByIssuerQuery(PoolKeys.GrantIssuer), ct) : [];
        var readyPools = grants.Where(x=>x.IsActive).Select(x=>PoolOperatorAccess.Parse(x.Value)).ToHashSet();
        foreach (var choice in choices)
        {
            var session = await catalog.SessionAsync(choice.SessionId, ct) ?? throw new PoolException("سانس یافت نشد", 404, "SESSION_NOT_FOUND");
            var pool = await catalog.PoolAsync(session.PoolId, ct) ?? throw new PoolException("استخر یافت نشد", 404);
            if (!pool.IsActive || !pool.IsSelling || !session.IsActive || !session.IsSelling
                || choice.AdultCount > 0 && !session.AdultEnabled || choice.ChildCount > 0 && !session.ChildEnabled)
                throw new PoolException("فروش انتخاب شما غیرفعال است", 409, "SESSION_UNAVAILABLE");
            if (options.CurrentValue.PurchaseEnabled && !readyPools.Contains(pool.Id))
                throw new PoolException("پذیرش این استخر هنوز آماده نیست",409,"POOL_NOT_READY");
            var provider = providers.Get(pool.ProviderKey);
            var quote = await provider.QuoteAsync(new(pool.ProviderKey, pool.ExternalId, session.ExternalRowId, choice.AdultCount, choice.ChildCount), ct);
            var range = times.Parse(quote.Date, quote.Time);
            if (range.Start is not DateTimeOffset starts || range.End is not DateTimeOffset ends || starts <= clock.GetUtcNow()
                || session.StartsAtUtc != starts || session.EndsAtUtc != ends)
                throw new PoolException("زمان سانس معتبر نیست یا تغییر کرده است", 409, "SESSION_TIME_CHANGED");
            if (quote.TotalMinor is not > 0) throw new PoolException("مبلغ معتبر دریافت نشد", 502, "INVALID_PRICE");
            result.Add(PoolRequest.Create(session.Id, pool.Id, pool.ProviderKey, pool.ExternalId, session.ExternalRowId,
                JsonSerializer.Serialize(new { pool = new { pool.Name, pool.State, pool.City, pool.ImageUrl }, session = new { session.Name, session.Date, session.Time, session.TermsHtml }, choice, quote }),
                pool.Name, quote.Date, quote.Time, choice.AdultCount, choice.ChildCount, quote.TotalMinor.Value, starts, ends));
        }
        return result;
    }
    public async Task ValidateAsync(PoolPurchase p, CancellationToken ct)
    {
        var resolved = await ResolveAsync(p.Requests.Select(r => new PoolChoice(r.SessionId, r.AdultCount, r.ChildCount)).ToArray(), ct);
        foreach (var r in p.Requests)
            if (resolved.Single(x => x.SessionId == r.SessionId).TotalMinor != r.TotalMinor)
                throw new PoolException("قیمت تغییر کرده است؛ خرید جدید را تأیید کنید", 409, "PRICE_CHANGED");
    }
}
