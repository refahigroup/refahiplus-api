namespace Refahi.Modules.Pools.Domain;

public interface IPoolCatalogRepository
{
    Task<PoolCatalogEntry?> PoolAsync(Guid id, CancellationToken ct);
    Task<PoolSessionEntry?> SessionAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PoolCatalogEntry>> ProviderPoolsAsync(string provider, CancellationToken ct);
    Task<IReadOnlyList<PoolSessionEntry>> PoolSessionsAsync(Guid poolId, CancellationToken ct);
    Task AddPoolAsync(PoolCatalogEntry pool, CancellationToken ct);
    Task AddSessionAsync(PoolSessionEntry session, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
