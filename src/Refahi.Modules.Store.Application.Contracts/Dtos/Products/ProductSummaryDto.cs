namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products;

public sealed record ProductSummaryDto(
    Guid Id, string Title, string Slug,
    long PriceMinor, long DiscountedPriceMinor,
    string ProductType, string DeliveryType, string SalesModel,
    string? MainImageUrl,
    bool IsAvailable);
