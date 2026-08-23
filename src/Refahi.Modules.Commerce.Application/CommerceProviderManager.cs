using Refahi.Modules.Commerce.Application.Contracts.Abstraction;
using System;
using System.Collections.Generic;
using System.Text;

namespace Refahi.Modules.Commerce.Application;

public class CommerceProviderManager : ICommerceProviderManager
{
    private Dictionary<string, ICommerceProvider> _providers = new ();


    public IReadOnlyCollection<ICommerceProvider> Providers => 
        _providers.Values.ToList().AsReadOnly();


    public void Register(ICommerceProvider provider)
    {
        if (_providers.ContainsKey(provider.Key))
            throw new Exception("Provider-Key already exists");

        _providers.Add(provider.Key, provider);
    }

    public void Unregister(ICommerceProvider provider)
    {
        if (_providers.ContainsKey(provider.Key))
            return;

        _providers.Remove(provider.Key);
    }
}
