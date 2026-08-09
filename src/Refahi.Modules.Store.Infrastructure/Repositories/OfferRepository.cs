using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public sealed class OfferRepository(StoreDbContext db) : IOfferRepository
{
    public Task<Offer?> GetByIdAsync(Guid id, bool includeDeleted = false, CancellationToken ct = default) =>
        db.Offers.FirstOrDefaultAsync(x => x.Id == id && (includeDeleted || !x.IsDeleted), ct);

    public Task<bool> HasOpenEndedCoordinateAsync(Guid productId, Guid shopId, Guid? variantId,
        Guid? sessionId, Guid? excludingId = null, CancellationToken ct = default) =>
        db.Offers.AnyAsync(x => x.ProductId == productId && x.ShopId == shopId &&
            x.ProductVariantId == variantId && x.ProductSessionId == sessionId &&
            !x.IsDeleted && x.EndDateUtc == null && (!excludingId.HasValue || x.Id != excludingId), ct);

    public async Task<(IReadOnlyList<Offer> Items, int Total)> GetPagedAsync(Guid? productId, Guid? shopId,
        bool includeDeleted, DateTimeOffset? effectiveAtUtc, int page, int size, CancellationToken ct = default)
    {
        var query = db.Offers.AsNoTracking().Where(x => includeDeleted || !x.IsDeleted);
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId);
        if (shopId.HasValue) query = query.Where(x => x.ShopId == shopId);
        if (effectiveAtUtc.HasValue)
        {
            var at = effectiveAtUtc.Value;
            query = query.Where(x => x.IsActive && !x.IsDeleted && x.StartDateUtc <= at &&
                (!x.EndDateUtc.HasValue || at < x.EndDateUtc.Value));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return (items, total);
    }

    public Task<Offer?> ResolveAsync(Guid productId, Guid shopId, Guid? variantId, Guid? sessionId,
        DateTimeOffset atUtc, CancellationToken ct = default) =>
        db.Offers.AsNoTracking().Where(x => x.ProductId == productId && x.ShopId == shopId &&
            x.ProductVariantId == variantId && x.ProductSessionId == sessionId && x.IsActive && !x.IsDeleted &&
            x.StartDateUtc <= atUtc && (!x.EndDateUtc.HasValue || atUtc < x.EndDateUtc.Value))
        .OrderBy(x => x.EndDateUtc == null)
        .ThenBy(x => x.EndDateUtc)
        .ThenByDescending(x => x.StartDateUtc)
        .ThenByDescending(x => x.CreatedAt)
        .ThenBy(x => x.Id)
        .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<OfferEligibilityCandidate>> GetEligibilityCandidatesAsync(
        Guid? productId, Guid? shopId, DateTimeOffset atUtc, CancellationToken ct = default)
    {
        var query =
            from offer in db.Offers.AsNoTracking()
            join product in db.Products.AsNoTracking() on offer.ProductId equals product.Id
            join shop in db.Shops.AsNoTracking() on offer.ShopId equals shop.Id
            where offer.IsActive && !offer.IsDeleted && offer.StartDateUtc <= atUtc &&
                  (!offer.EndDateUtc.HasValue || atUtc < offer.EndDateUtc.Value) &&
                  product.IsAvailable && !product.IsDeleted && product.SupplierId != Guid.Empty &&
                  shop.Status == ShopStatus.Active && product.SupplierId == shop.SupplierId
            select new { offer, product, shop };
        if (productId.HasValue) query = query.Where(x => x.offer.ProductId == productId.Value);
        if (shopId.HasValue) query = query.Where(x => x.offer.ShopId == shopId.Value);
        return await query.OrderByDescending(x => x.offer.CreatedAt).ThenBy(x => x.offer.Id)
            .Select(x => new OfferEligibilityCandidate(x.offer.Id, x.product.SupplierId,
                x.product.CategoryId, (short)x.shop.ShopType))
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<Offer> Items, int Total)> GetPageByIdsAsync(
        IReadOnlyCollection<Guid> eligibleIds, int page, int size, CancellationToken ct = default)
    {
        if (eligibleIds.Count == 0) return ([], 0);
        var query = db.Offers.AsNoTracking().Where(x => eligibleIds.Contains(x.Id));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return (items, total);
    }

    public async Task AddAsync(Offer offer, CancellationToken ct = default)
    {
        db.Offers.Add(offer);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_offers_open_coordinate", StringComparison.OrdinalIgnoreCase) == true)
        { throw new StoreDomainException("برای این مختصات یک پیشنهاد بدون پایان وجود دارد", "OPEN_OFFER_ALREADY_EXISTS"); }
    }

    public async Task UpdateAsync(Offer offer, uint expectedVersion, CancellationToken ct = default)
    {
        db.Entry(offer).Property(x => x.Version).OriginalValue = expectedVersion;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new StoreConcurrencyException(ex); }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_offers_open_coordinate", StringComparison.OrdinalIgnoreCase) == true)
        { throw new StoreDomainException("برای این مختصات یک پیشنهاد بدون پایان وجود دارد", "OPEN_OFFER_ALREADY_EXISTS"); }
    }
}
