namespace Refahi.Modules.Pools.Application.Contracts;

public interface IPoolProvider
{
    string Key { get; }
    string Name { get; }
    PoolCapabilities Capabilities { get; }

    Task<IReadOnlyList<PoolState>> StatesAsync(bool activeOnly, CancellationToken ct);
    Task<IReadOnlyList<PoolCity>> CitiesAsync(string? stateId, bool activeOnly, CancellationToken ct);
    Task<PoolCatalog> SearchAsync(PoolSearchQuery query, CancellationToken ct);
    Task<IReadOnlyList<PoolSession>> SessionsAsync(string poolId, CancellationToken ct);
    Task<string> TermsAsync(string poolId, CancellationToken ct);
    Task<PoolQuote> QuoteAsync(PoolSelection selection, CancellationToken ct);
}
