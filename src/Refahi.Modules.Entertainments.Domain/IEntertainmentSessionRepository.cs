namespace Refahi.Modules.Entertainments.Domain;

public interface IEntertainmentSessionRepository
{
    Task<EntertainmentCheckoutSession?> GetFreshAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<EntertainmentCheckoutSession>> GetUncertainAsync(CancellationToken ct);
    Task<EntertainmentCheckoutSession?> GetAsync(Guid id, CancellationToken ct);
    Task<EntertainmentCheckoutSession?> FindAsync(Guid userId, string key, CancellationToken ct);
    Task AddAsync(EntertainmentCheckoutSession session, CancellationToken ct);
    Task<IReadOnlyList<EntertainmentCheckoutSession>> GetExpiredAsync(CancellationToken ct);
    Task<IReadOnlyList<EntertainmentCheckoutSession>> GetForRedactionAsync(DateTimeOffset createdBefore, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
