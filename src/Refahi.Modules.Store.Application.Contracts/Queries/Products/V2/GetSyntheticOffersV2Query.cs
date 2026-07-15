using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products.V2;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Products.V2;

public sealed record GetSyntheticOffersV2Query(
    int ModuleId,
    string? SearchQuery = null,
    int? CategoryId = null,
    Guid? ShopId = null,
    string? ShopSlug = null,
    Guid? ProductId = null,
    string? ProductSlug = null,
    string? SalesModel = null,
    string? OfferKind = null,
    DateOnly? UsageFrom = null,
    DateOnly? UsageTo = null,
    long? MinPriceMinor = null,
    long? MaxPriceMinor = null,
    string Sort = "newest",
    int PageNumber = 1,
    int PageSize = 30)
    : IRequest<SyntheticOffersV2PagedResponse?>;

public sealed record SyntheticOffersV2PagedResponse(
    IReadOnlyList<SyntheticOfferDto> Data,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);

