namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products;

public sealed record ProductVariantDto(
    Guid Id, string? Size, string? Color, string? ColorHex,
    string? ImageUrl, int StockCount, long PriceAdjustment, bool IsAvailable);
