using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Domain.Repositories;

public sealed record GeneratedVoucherMaterial(int SequenceNumber, string CodeHash, string CodeCiphertext);
public sealed record StoreOrderPaidContext(
    string OrderNumber,
    string SupplierName,
    string ShopName,
    DateTimeOffset PaidAtUtc,
    IReadOnlyDictionary<Guid, IReadOnlyList<GeneratedVoucherMaterial>> GeneratedMaterials);

public interface IStoreOrderRepository
{
    Task<StoreOrder?> GetByIdempotencyKeyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken ct = default
    );
    Task<StoreOrder?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<StoreOrder?> GetByIdAsync(Guid storeOrderId, CancellationToken ct = default) =>
        Task.FromResult<StoreOrder?>(null);
    Task<IReadOnlyList<StoreOrder>> GetByOrderIdsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<StoreOrder>>([]);
    Task<bool> UserHasPurchasedProductAsync(
        Guid userId,
        Guid productId,
        CancellationToken ct = default
    ) => Task.FromResult(false);
    Task AddAsync(StoreOrder order, CancellationToken ct = default);
    Task UpdateAsync(StoreOrder order, CancellationToken ct = default);
    [Obsolete("Use the atomic finalization overload with StoreOrderPaidContext.")]
    Task CommitPaidAsync(Guid orderId, CancellationToken ct = default);
    Task CommitPaidAsync(Guid orderId, StoreOrderPaidContext context, CancellationToken ct = default) =>
        CommitPaidAsync(orderId, ct);
}
