namespace Refahi.Modules.Pools.Domain;

public interface IPoolPurchaseRepository
{
    Task<PoolPurchase?> GetAsync(Guid id, CancellationToken ct);
    Task<PoolPurchase?> FindAsync(Guid userId, string key, CancellationToken ct);
    Task<PoolPurchase?> FindByCodeAsync(string hash, CancellationToken ct);
    Task<PoolRequest?> FindRedemptionAsync(Guid userId, string key, CancellationToken ct);
    Task AddAsync(PoolPurchase purchase, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<IReadOnlyList<Guid>> RecoveryIdsAsync(DateTimeOffset? after, Guid? afterId, CancellationToken ct);
}
