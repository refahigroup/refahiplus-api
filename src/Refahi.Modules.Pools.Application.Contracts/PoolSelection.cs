namespace Refahi.Modules.Pools.Application.Contracts;

public sealed record PoolSelection(
    string ProviderKey,
    string PoolId,
    string SessionRowId,
    int AdultCount,
    int ChildCount
);
