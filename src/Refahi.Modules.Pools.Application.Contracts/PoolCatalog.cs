namespace Refahi.Modules.Pools.Application.Contracts;

public sealed record PoolCatalog(
    IReadOnlyList<PoolSummary> Items,
    int Page,
    int Limit,
    IReadOnlyList<PoolSort> Sorts,
    string? SortedBy
);
