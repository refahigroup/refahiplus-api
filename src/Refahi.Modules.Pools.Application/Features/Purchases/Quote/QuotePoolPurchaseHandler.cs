using MediatR;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Features.Purchases;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Quote;
public sealed class QuotePoolPurchaseHandler(PoolQuoteResolver quotes, IOptionsMonitor<PoolsOptions> options) : IRequestHandler<QuotePoolPurchaseQuery, PoolQuoteDto>
{
    public async Task<PoolQuoteDto> Handle(QuotePoolPurchaseQuery r, CancellationToken ct)
    {
        var flags = options.CurrentValue;
        if (flags.CatalogEnabled && !flags.PurchaseEnabled)
            throw new PoolException("خرید استخر فعال نیست", 503, "PURCHASE_DISABLED");
        var rows = await quotes.ResolveAsync(r.Choices, ct); return new(rows.Select(x => new PoolQuoteLine(x.SessionId, x.TotalMinor)).ToArray(), rows.Aggregate(0L, (sum, x) => checked(sum + x.TotalMinor)));
    }
}
