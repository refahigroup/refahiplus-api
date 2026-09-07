using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Infrastructure;

public sealed class CommerceProviderFactory(IEnumerable<ICommerceProvider> providers) : ICommerceProviderFactory
{
    private readonly IReadOnlyDictionary<string, ICommerceProvider> values = providers.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ICommerceProvider> GetEnabledProviders() => 
        values.Values.ToArray();

    public ICommerceProvider GetRequired(string providerKey) => 
        values.TryGetValue(providerKey, out var provider)
            ? provider 
            : throw new CommerceDomainException("تامین‌کننده Commerce یافت نشد", "PROVIDER_NOT_FOUND");
}
