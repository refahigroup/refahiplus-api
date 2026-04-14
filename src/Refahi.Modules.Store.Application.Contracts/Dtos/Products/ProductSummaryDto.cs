namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products;

public sealed record ProductSummaryDto(
    Guid Id, string Title, string Slug,
    long PriceMinor, long? DiscountedPriceMinor, int? DiscountPercent,
    string ProductType, string DeliveryType, string SalesModel,
    string? MainImageUrl,
    string ShopName, string? City,
    bool IsAvailable);
