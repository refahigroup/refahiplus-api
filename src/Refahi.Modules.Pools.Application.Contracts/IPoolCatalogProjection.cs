namespace Refahi.Modules.Pools.Application.Contracts;
public interface IPoolCatalogProjection
{
    Task VerifyIdentityAsync(Guid venueId, string name, string state, string city, PoolSummary summary, CancellationToken ct);
    Task RefreshAsync(Guid venueId, PoolSummary summary, CancellationToken ct);
    Task CompleteAsync(string provider, CancellationToken ct);
}
