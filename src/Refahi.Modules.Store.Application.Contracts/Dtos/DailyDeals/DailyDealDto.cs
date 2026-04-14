namespace Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;

public sealed record DailyDealDto(
    int Id, Guid ProductId, string ProductTitle, string? ProductImageUrl,
    long OriginalPriceMinor, int DiscountPercent, long DiscountedPriceMinor,
    DateTimeOffset StartTime, DateTimeOffset EndTime,
    string ShopName);
