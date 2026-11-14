namespace Refahi.Modules.Pools.Application.Contracts;

public interface IPoolProviderFactory
{
    IReadOnlyList<IPoolProvider> Enabled();
    IPoolProvider Get(string key);
}
