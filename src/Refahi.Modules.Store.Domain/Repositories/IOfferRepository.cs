using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IOfferRepository
{
    Task<Offer?> GetByIdAsync(Guid id, bool includeDeleted = false, CancellationToken ct = default);
    Task<bool> HasOpenEndedCoordinateAsync(Guid productId, Guid shopId, Guid? variantId,
        Guid? sessionId, Guid? excludingId = null, CancellationToken ct = default);
    Task<(IReadOnlyList<Offer> Items, int Total)> GetPagedAsync(Guid? productId, Guid? shopId,
        bool includeDeleted, DateTimeOffset? effectiveAtUtc, int page, int size, CancellationToken ct = default);
    Task<IReadOnlyList<OfferEligibilityCandidate>> GetEligibilityCandidatesAsync(Guid? productId,
        Guid? shopId, DateTimeOffset atUtc, CancellationToken ct = default);
    Task<(IReadOnlyList<Offer> Items, int Total)> GetPageByIdsAsync(IReadOnlyCollection<Guid> eligibleIds,
        int page, int size, CancellationToken ct = default);
    Task<Offer?> ResolveAsync(Guid productId, Guid shopId, Guid? variantId, Guid? sessionId,
        DateTimeOffset atUtc, CancellationToken ct = default);
    Task AddAsync(Offer offer, CancellationToken ct = default);
    Task UpdateAsync(Offer offer, uint expectedVersion, CancellationToken ct = default);
}

public sealed record OfferEligibilityCandidate(
    Guid OfferId, Guid SupplierId, int CategoryId, short SalesChannel);
