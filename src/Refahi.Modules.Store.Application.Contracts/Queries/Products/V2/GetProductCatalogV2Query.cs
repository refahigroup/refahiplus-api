using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products.V2;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Products.V2;

public sealed record GetProductCatalogV2Query(
    int ModuleId,
    string? SearchQuery = null,
    int? CategoryId = null,
    Guid? ShopId = null,
    string? ShopSlug = null,
    string? SalesModel = null,
    long? MinPriceMinor = null,
    long? MaxPriceMinor = null,
    string Sort = "newest",
    int PageNumber = 1,
    int PageSize = 30)
    : IRequest<ProductCatalogV2PagedResponse?>;

public sealed record ProductCatalogV2PagedResponse(
    IReadOnlyList<ProductCatalogItemV2Dto> Data,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);

