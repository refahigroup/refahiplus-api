namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public interface ICommerceProviderFactory
{
    IReadOnlyCollection<ICommerceProvider> GetEnabledProviders();
    ICommerceProvider GetRequired(string providerKey);
}
