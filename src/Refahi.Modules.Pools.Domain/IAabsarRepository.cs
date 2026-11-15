namespace Refahi.Modules.Pools.Domain;

public interface IAabsarRepository
{
    Task<AabsarCart?> CartAsync(Guid userId, CancellationToken ct);
    Task<AabsarFulfillment?> FulfillmentAsync(Guid purchaseId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, AabsarShowtime>> ShowtimesAsync(IEnumerable<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<AabsarFulfillment>> UnresolvedAsync(int limit, CancellationToken ct);
    void Add(AabsarCart cart);
    void Add(AabsarFulfillment fulfillment);
    Task RedactContactsAsync(DateTimeOffset cutoff, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
