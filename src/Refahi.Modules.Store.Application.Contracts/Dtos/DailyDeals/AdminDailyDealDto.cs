namespace Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;

public sealed record AdminDailyDealDto(
    int Id, int? ModuleId, Guid? ShopId,
    Guid ProductId, string ProductTitle, string? ProductImageUrl,
    long OriginalPriceMinor, int DiscountPercent, long DiscountedPriceMinor,
    DateTimeOffset StartTime, DateTimeOffset EndTime,
    bool IsActive, string ShopName);
