using System;
using System.Collections.Generic;
using System.Text;

namespace Refahi.Modules.Commerce.Application.Contracts.Abstraction;

public interface ICommerceProviderManager
{
    IReadOnlyCollection<ICommerceProvider> Providers { get; }

    void Register(ICommerceProvider provider);
    void Unregister(ICommerceProvider provider);

}
