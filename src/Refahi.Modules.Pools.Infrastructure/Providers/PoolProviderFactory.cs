using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
namespace Refahi.Modules.Pools.Infrastructure.Providers;
public sealed class PoolProviderFactory(IEnumerable<IPoolProvider> providers, IOptionsMonitor<PoolsOptions> options) : IPoolProviderFactory
{
    public IReadOnlyList<IPoolProvider> Enabled() => options.CurrentValue.CatalogEnabled ? providers.ToArray() : [];
    public IPoolProvider Get(string key) => Enabled().SingleOrDefault(x => x.Key == key) ?? throw new PoolException("سرویس استخر فعال نیست", 503);
}
