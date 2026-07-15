using Refahi.Modules.Store.Application.Contracts.Dtos.Products;

namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products.V2;

public sealed record ProductDetailV2Dto(
    Guid Id,
    string Title,
    string Slug,
    string? Description,
    string ProductType,
    string DeliveryType,
    string SalesModel,
    int? CategoryId,
    string? CategoryName,
    bool IsAvailable,
    string PriceDisplayMode,
    long MinEffectivePriceMinor,
    long MaxEffectivePriceMinor,
    string DefaultOfferKey,
    SelectedShopV2Dto SelectedShop,
    List<ProductImageDto> Images,
    List<ProductSpecificationDto> Specifications,
    List<SyntheticOfferDto> Offers,
    double AverageRating,
    int ReviewCount,
    DateTimeOffset CreatedAt);

public sealed record SelectedShopV2Dto(Guid Id, string Name, string Slug);

