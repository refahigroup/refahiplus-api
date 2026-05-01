using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Queries.ShopProducts;

public sealed record GetShopProductsQuery(
    Guid ShopId,
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<ShopProductsPagedResponse>;

public sealed record ShopProductDto(
    Guid Id,
    Guid ShopId,
    Guid ProductId,
    string ProductTitle,
    string? ProductImageUrl,
    long Price,
    long DiscountedPrice,
    decimal CommissionPercent,
    long CommissionPrice,
    string? Description,
    bool IsActive,
    bool IsDeleted,
    DateTimeOffset CreatedAt);

public sealed record ShopProductsPagedResponse(
    IEnumerable<ShopProductDto> Data,
    int PageNumber, int PageSize, int TotalCount, int TotalPages);
