using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Domain.Services;

public static class OfferResolver
{
    public static Offer? Select(IEnumerable<Offer> candidates, DateTimeOffset atUtc) => candidates
        .Where(x => x.IsEffectiveAt(atUtc))
        .OrderBy(x => x.EndDateUtc == null)
        .ThenBy(x => x.EndDateUtc)
        .ThenByDescending(x => x.StartDateUtc)
        .ThenByDescending(x => x.CreatedAt)
        .ThenBy(x => x.Id)
        .FirstOrDefault();
}
