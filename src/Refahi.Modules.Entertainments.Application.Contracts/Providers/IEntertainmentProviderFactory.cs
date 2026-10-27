namespace Refahi.Modules.Entertainments.Application.Contracts.Providers;

public interface IEntertainmentProviderFactory
{
    IReadOnlyCollection<IEntertainmentProvider> GetEnabledProviders();
    IEntertainmentProvider GetRequired(string providerKey);
}
