using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Infrastructure.Providers;
public sealed class FixturePoolProvider(IOptions<MockPoolOptions> mock) : IPoolProvider
{
    public string Key => "poolticket";
    public string Name => "پول تیکت — داده آزمایشی";
    public PoolCapabilities Capabilities => new(true, true, false, false, false, false, true);
    private static PoolSummary Pool => new("poolticket", "1", "استخر آزمایشی", null, "تهران", "تهران", null, null, null, true, 2, null, 0, 1000000, 1000000, "IRR");
    public Task<IReadOnlyList<PoolState>> StatesAsync(bool active, CancellationToken ct) => Task.FromResult<IReadOnlyList<PoolState>>([new("1", "تهران")]);
    public Task<IReadOnlyList<PoolCity>> CitiesAsync(string? state, bool active, CancellationToken ct) => Task.FromResult<IReadOnlyList<PoolCity>>(state is null or "1" ? [new("1", "تهران", "1", "تهران")] : []);
    public Task<PoolCatalog> SearchAsync(PoolSearchQuery q, CancellationToken ct) => Task.FromResult(new PoolCatalog(
        q.Page == 1 && (string.IsNullOrEmpty(q.Search) || Pool.Name.Contains(q.Search)) && q.CityId is null or "1" ? [Pool] : [], q.Page, q.Limit, [new("name", "نام")], q.Sort));
    public Task<IReadOnlyList<PoolSession>> SessionsAsync(string pool, CancellationToken ct)
    {
        if (pool != "1") throw new PoolException("استخر آزمایشی یافت نشد", 404);
        return Task.FromResult<IReadOnlyList<PoolSession>>([new("1", "سانس آزمایشی", 0, [new("1", true, true, true,
            1000000, 500000, 1000000, 500000, "تاریخ آزمایشی", "", "۱۰ تا ۱۲", "", "<p>آزمایشی؛ فاقد اعتبار ورود</p>", "IRR")])]);
    }
    public Task<string> TermsAsync(string id, CancellationToken ct) => id == "1" ? Task.FromResult("<p>آزمایشی؛ فاقد اعتبار ورود</p>") : throw new PoolException("استخر یافت نشد", 404);
    public async Task<PoolQuote> QuoteAsync(PoolSelection s, CancellationToken ct)
    {
        var row = (await SessionsAsync(s.PoolId, ct)).SelectMany(x => x.Rows).SingleOrDefault(x => x.Id == s.SessionRowId) ?? throw new PoolException("سانس یافت نشد", 404);
        var amount = checked(s.AdultCount * 1000000L + s.ChildCount * 500000L + (mock.Value.Scenario == "PriceChange" ? 100000 : 0));
        return new(row.Id, s.AdultCount, s.ChildCount, amount, amount, amount, "IRR", row.Date, row.Day, row.Time);
    }
}
