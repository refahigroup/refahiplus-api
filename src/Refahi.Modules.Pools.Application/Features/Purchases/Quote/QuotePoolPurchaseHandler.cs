using MediatR;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Features.Purchases;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Quote;
public sealed class QuotePoolPurchaseHandler(PoolQuoteResolver quotes) : IRequestHandler<QuotePoolPurchaseQuery, PoolQuoteDto>
{
    public async Task<PoolQuoteDto> Handle(QuotePoolPurchaseQuery r, CancellationToken ct)
    { var rows = await quotes.ResolveAsync(r.Choices, ct); return new(rows.Select(x => new PoolQuoteLine(x.SessionId, x.TotalMinor)).ToArray(), rows.Aggregate(0L, (sum, x) => checked(sum + x.TotalMinor))); }
}
