using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Infrastructure.Providers;
public sealed class PoolPurchasePolicy(IHostEnvironment environment, IOptions<PoolsOptions> options) : IPoolPurchasePolicy
{
    public static bool IsTestEnvironment(IHostEnvironment env) => env.IsDevelopment() || env.IsStaging() || env.IsEnvironment("Test");
    public void EnsureAllowed(bool isMock)
    {
        if (!options.Value.PurchaseEnabled || isMock && (!IsTestEnvironment(environment) || !options.Value.MockLifecycleEnabled || !options.Value.TestFundsConfirmed))
            throw new PoolException("خرید استخر در این محیط فعال نیست", 503);
        if (!isMock) throw new PoolException("اتصال خرید واقعی هنوز پیاده‌سازی نشده است", 503);
    }
}
public sealed class PoolProviderFactory(IEnumerable<IPoolProvider> providers, IOptions<PoolsOptions> options) : IPoolProviderFactory
{
    public IReadOnlyList<IPoolProvider> Enabled() => options.Value.CatalogEnabled ? providers.ToArray() : [];
    public IPoolProvider Get(string key) => Enabled().SingleOrDefault(x => x.Key == key)
        ?? throw new PoolException("تأمین‌کننده استخر فعال نیست", 404);
}
