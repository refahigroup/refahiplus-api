using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;

public sealed record BackfillShopProductVariantsCommand(
    Guid? ShopId = null,
    Guid? ProductId = null,
    bool DryRun = true,
    int DetailLimit = 100) : IRequest<ShopProductVariantBackfillResultDto>;

public sealed record ShopProductVariantBackfillResultDto(
    bool DryRun,
    int ShopProductsChecked,
    int ProductsWithVariants,
    int CreatedOfferings,
    int SkippedExistingOfferings,
    int SkippedInvalidVariants,
    IReadOnlyList<ShopProductVariantBackfillCreatedItemDto> CreatedItems,
    IReadOnlyList<string> Warnings);

public sealed record ShopProductVariantBackfillCreatedItemDto(
    Guid ShopId,
    string ShopName,
    Guid ProductId,
    string ProductName,
    Guid ShopProductId,
    Guid ProductVariantId,
    string VariantName,
    long PriceMinor,
    long? DiscountedPriceMinor);
