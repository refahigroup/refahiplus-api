namespace Refahi.Modules.Pools.Application.Contracts;

public sealed record PoolSession(
    string Id,
    string Name,
    int Gender,
    IReadOnlyList<PoolSessionRow> Rows
);
