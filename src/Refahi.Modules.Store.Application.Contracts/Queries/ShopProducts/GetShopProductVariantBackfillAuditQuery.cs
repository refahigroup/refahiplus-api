using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Queries.ShopProducts;

public sealed record GetShopProductVariantBackfillAuditQuery(
    Guid? ShopId = null,
    Guid? ProductId = null,
    int DetailLimit = 100) : IRequest<ShopProductVariantBackfillAuditDto>;

public sealed record ShopProductVariantBackfillAuditDto(
    int ShopProductsChecked,
    int ProductsWithVariants,
    int ExistingOfferings,
    int MissingOfferings,
    IReadOnlyList<ShopProductVariantBackfillAuditItemDto> Items);

public sealed record ShopProductVariantBackfillAuditItemDto(
    Guid ShopId,
    string ShopName,
    Guid ProductId,
    string ProductName,
    Guid ShopProductId,
    int VariantCount,
    int ExistingOfferingCount,
    int MissingOfferingCount);
