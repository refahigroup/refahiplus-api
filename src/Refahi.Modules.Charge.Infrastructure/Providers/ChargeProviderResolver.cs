using Refahi.Modules.Charge.Application.Contracts.Providers;

namespace Refahi.Modules.Charge.Infrastructure.Providers;

public sealed class ChargeProviderResolver : IChargeProviderResolver
{
    private readonly IEnumerable<IChargeProvider> _providers;

    public ChargeProviderResolver(IEnumerable<IChargeProvider> providers)
    {
        _providers = providers;
    }

    public IChargeProvider Get(string providerName) =>
        _providers.FirstOrDefault(x => x.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase)) ??
            throw new InvalidOperationException($"تامین‌کننده شارژ {providerName} ثبت نشده است");

    public IChargeProvider GetDefault() => Get("Eniac");
}
