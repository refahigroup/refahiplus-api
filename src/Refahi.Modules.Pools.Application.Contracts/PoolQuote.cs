namespace Refahi.Modules.Pools.Application.Contracts;

public sealed record PoolQuote(
    string RowId,
    int AdultCount,
    int ChildCount,
    decimal SaleTotal,
    decimal MarketTotal,
    long? TotalMinor,
    string? PriceUnit,
    string Date,
    string Day,
    string Time
);
