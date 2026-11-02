using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Infrastructure;

public sealed class EntertainmentProviderFactory(IEnumerable<IEntertainmentProvider> providers) : IEntertainmentProviderFactory
{
    private readonly IReadOnlyDictionary<string, IEntertainmentProvider> values = providers.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IEntertainmentProvider> GetEnabledProviders() => 
        values.Values.ToArray();

    public IEntertainmentProvider GetRequired(string providerKey) => 
        values.TryGetValue(providerKey, out var provider)
            ? provider 
            : throw new EntertainmentDomainException("تامین‌کننده Entertainment یافت نشد", "PROVIDER_NOT_FOUND");
}
