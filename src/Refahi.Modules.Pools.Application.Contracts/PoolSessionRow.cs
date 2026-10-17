namespace Refahi.Modules.Pools.Application.Contracts;

public sealed record PoolSessionRow(
    string Id,
    bool IsSelling,
    bool AdultEnabled,
    bool ChildEnabled,
    decimal AdultPrice,
    decimal ChildPrice,
    decimal AdultMarketPrice,
    decimal ChildMarketPrice,
    string Date,
    string Day,
    string Time,
    string DescriptionHtml,
    string TermsHtml,
    string? PriceUnit
);
