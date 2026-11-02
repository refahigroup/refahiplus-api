namespace Refahi.Modules.Commerce.Domain;

public interface ICommerceSessionRepository
{
    Task<CommerceCheckoutSession?> GetAsync(Guid id, CancellationToken ct);
    Task<CommerceCheckoutSession?> FindAsync(Guid userId, string key, CancellationToken ct);
    Task AddAsync(CommerceCheckoutSession session, CancellationToken ct);
    Task<IReadOnlyList<CommerceCheckoutSession>> GetExpiredAsync(IReadOnlyCollection<string> providerKeys, CancellationToken ct);
    Task<IReadOnlyList<CommerceCheckoutSession>> GetForRedactionAsync(DateTimeOffset createdBefore, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
