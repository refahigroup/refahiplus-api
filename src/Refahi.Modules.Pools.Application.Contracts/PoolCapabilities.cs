namespace Refahi.Modules.Pools.Application.Contracts;

public sealed record PoolCapabilities(
    bool Discovery,
    bool Quote,
    bool Reserve,
    bool Confirm,
    bool Status,
    bool Cancel,
    bool IsMock
);
