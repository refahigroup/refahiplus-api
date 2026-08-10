using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Products.V3;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementCategoryTerms;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.CatalogV3;

public sealed class GetPublicProductCatalogV3Handler(
    IPublicCatalogRepository catalog,
    IStoreModuleRepository modules,
    IMediator mediator,
    IPathService pathService
) : IRequestHandler<GetPublicProductCatalogV3Query, PublicProductCatalogV3Page?>
{
    private readonly IPathService _pathService = pathService;
    public async Task<PublicProductCatalogV3Page?> Handle(
        GetPublicProductCatalogV3Query request,
        CancellationToken ct
    )
    {
        var module = await modules.GetBySlugAsync(
            NormalizeRequired(request.ModuleSlug, "اسلاگ ماژول الزامی است"),
            ct
        );
        if (module is null || !module.IsActive)
            return null;
        ValidatePaging(request.Page, request.PageSize);
        ValidatePriceRange(request.MinPriceMinor, request.MaxPriceMinor);
        var salesModel = ParseSalesModel(request.SalesModel);

        var atUtc = DateTimeOffset.UtcNow;
        var candidates = await catalog.GetEffectiveCandidatesAsync(
            module.CategoryId,
            request.CategoryId,
            request.ShopId,
            request.ShopSlug,
            request.Search,
            salesModel,
            atUtc,
            ct
        );
        var eligible = await PublicCatalogEligibility.FilterAsync(candidates, mediator, atUtc, ct);
        var priceScoped = eligible.Where(x =>
            (!request.MinPriceMinor.HasValue || x.FinalPriceMinor >= request.MinPriceMinor)
            && (!request.MaxPriceMinor.HasValue || x.FinalPriceMinor <= request.MaxPriceMinor)
        );
        var groups = priceScoped.GroupBy(x => x.ProductId).Select(x => PublicCatalogMapping.MapGroup(x, _pathService));

        groups = NormalizeSort(request.Sort) switch
        {
            "price-asc" => groups.OrderBy(x => x.Price.MinFinalPriceMinor).ThenBy(x => x.ProductId),
            "price-desc" => groups
                .OrderByDescending(x => x.Price.MinFinalPriceMinor)
                .ThenBy(x => x.ProductId),
            _ => groups.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.ProductId),
        };
        var materialized = groups.ToArray();
        return new(
            materialized
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToArray(),
            materialized.Length,
            request.Page,
            request.PageSize
        );
    }

    internal static string NormalizeSort(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "price-asc" => "price-asc",
            "price-desc" => "price-desc",
            _ => "newest",
        };

    internal static SalesModel? ParseSalesModel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim().ToLowerInvariant() switch
        {
            "stockbased" or "inventorybased" => SalesModel.InventoryBased,
            "sessionbased" => SalesModel.SessionBased,
            "unlimited" => SalesModel.Unlimited,
            _ => throw new StoreDomainException("مدل فروش نامعتبر است", "INVALID_SALES_MODEL"),
        };
    }

    private static string NormalizeRequired(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new StoreDomainException(message, "MODULE_SLUG_REQUIRED");
        return value.Trim().ToLowerInvariant();
    }

    private static void ValidatePaging(int page, int size)
    {
        if (page < 1 || size is < 1 or > 100)
            throw new StoreDomainException(
                "شماره صفحه یا اندازه صفحه نامعتبر است",
                "INVALID_PAGING"
            );
    }

    private static void ValidatePriceRange(long? min, long? max)
    {
        if (min < 0 || max < 0 || (min.HasValue && max.HasValue && min > max))
            throw new StoreDomainException("بازه قیمت نامعتبر است", "INVALID_PRICE_RANGE");
    }
}

public sealed class GetPublicProductDetailV3Handler(
    IPublicCatalogRepository catalog,
    IStoreModuleRepository modules,
    IProductRepository products,
    IMediator mediator,
    IPathService pathService
) : IRequestHandler<GetPublicProductDetailV3Query, PublicProductDetailV3Dto?>
{
    private readonly IPathService _pathService = pathService;
    public async Task<PublicProductDetailV3Dto?> Handle(
        GetPublicProductDetailV3Query request,
        CancellationToken ct
    )
    {
        if (
            string.IsNullOrWhiteSpace(request.ModuleSlug)
            || string.IsNullOrWhiteSpace(request.ProductSlug)
        )
            throw new StoreDomainException("اسلاگ ماژول و محصول الزامی است", "SLUG_REQUIRED");
        var module = await modules.GetBySlugAsync(request.ModuleSlug.Trim().ToLowerInvariant(), ct);
        if (module is null || !module.IsActive)
            return null;
        var atUtc = DateTimeOffset.UtcNow;
        var candidates = await catalog.GetEffectiveCandidatesAsync(
            module.CategoryId,
            null,
            request.ShopId,
            request.ShopSlug,
            null,
            null,
            atUtc,
            ct
        );
        var eligible = (await PublicCatalogEligibility.FilterAsync(candidates, mediator, atUtc, ct))
            .Where(x =>
                x.ProductSlug.Equals(request.ProductSlug.Trim(), StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();
        if (eligible.Length == 0)
            return null;

        var selectedCandidates = eligible.AsEnumerable();
        if (request.OfferId.HasValue)
            selectedCandidates = selectedCandidates.Where(x => x.OfferId == request.OfferId);
        if (request.VariantId.HasValue)
            selectedCandidates = selectedCandidates.Where(x =>
                x.ProductVariantId == request.VariantId
            );
        if (request.SessionId.HasValue)
            selectedCandidates = selectedCandidates.Where(x =>
                x.ProductSessionId == request.SessionId
            );
        var selected = selectedCandidates
            .OrderBy(x => x.FinalPriceMinor)
            .ThenBy(x => x.OfferId)
            .FirstOrDefault();
        if (
            (request.OfferId.HasValue || request.VariantId.HasValue || request.SessionId.HasValue)
            && selected is null
        )
            return null;
        selected ??= eligible.OrderBy(x => x.FinalPriceMinor).ThenBy(x => x.OfferId).First();

        var product = await products.GetByIdAsync(selected.ProductId, ct);
        if (product is null || product.SupplierId == Guid.Empty)
            return null;
        var offers = eligible
            .OrderBy(x => x.FinalPriceMinor)
            .ThenBy(x => x.ShopSlug)
            .ThenBy(x => x.OfferId)
            .Select(PublicCatalogMapping.MapOffer)
            .ToArray();
        var summary = PublicCatalogMapping.MapPrice(eligible);
        var dto = CatalogV3Mapper.Map(product, (short)SalesChannel.Online);
        return new(
            dto,
            product
                .Images.OrderBy(x => x.SortOrder)
                .Select(x => new ProductImageDto(
                    x.Id,
                    _pathService.MakeAbsoluteMediaUrl(x.ImageUrl),
                    x.IsMain,
                    x.SortOrder
                ))
                .ToArray(),
            product
                .Specifications.OrderBy(x => x.SortOrder)
                .Select(x => new ProductSpecificationDto(x.Id, x.Key, x.Value, x.SortOrder))
                .ToArray(),
            product
                .VariantAttributes.OrderBy(x => x.SortOrder)
                .Select(x => new VariantAttributeDto(
                    x.Id,
                    x.Name,
                    x.SortOrder,
                    x.Values.OrderBy(v => v.SortOrder)
                        .Select(v => new VariantAttributeValueDto(v.Id, v.Value, v.SortOrder))
                        .ToList()
                ))
                .ToArray(),
            product.Variants.Select(x => CatalogV3Mapper.MapVariant(product, x, _pathService)).ToArray(),
            product.Sessions.Select(CatalogV3Mapper.MapSession).ToArray(),
            offers,
            summary,
            selected.OfferId,
            "Offer"
        );
    }
}

internal static class PublicCatalogEligibility
{
    public static async Task<IReadOnlyList<PublicCatalogOfferCandidate>> FilterAsync(
        IReadOnlyList<PublicCatalogOfferCandidate> candidates,
        IMediator mediator,
        DateTimeOffset atUtc,
        CancellationToken ct
    )
    {
        if (candidates.Count == 0)
            return [];
        var requests = candidates
            .Select(x => new AgreementCategoryTermResolutionRequest(
                x.SupplierId,
                x.CategoryId,
                (short)SalesChannel.Online,
                atUtc
            ))
            .Distinct()
            .ToArray();
        var allowed = new HashSet<(Guid SupplierId, int CategoryId)>();
        foreach (var chunk in requests.Chunk(1000))
        {
            var resolved = await mediator.Send(
                new ResolveAgreementCategoryTermsBatchQuery(chunk),
                ct
            );
            foreach (var hit in resolved.Where(x => x.Term is not null))
                allowed.Add((hit.Request.SupplierId, hit.Request.CategoryId));
        }
        return candidates.Where(x => allowed.Contains((x.SupplierId, x.CategoryId))).ToArray();
    }
}

internal static class PublicCatalogMapping
{
    public static PublicProductCatalogItemV3Dto MapGroup(
        IGrouping<Guid, PublicCatalogOfferCandidate> group,
        IPathService pathService
    )
    {
        var first = group.First();
        return new(
            first.ProductId,
            first.ProductTitle,
            first.ProductSlug,
            first.ProductDescription,
            first.MainImageUrl is null ? null : pathService.MakeAbsoluteMediaUrl(first.MainImageUrl),
            (short)first.ProductType,
            (short)first.SalesModel,
            (short)first.FulfillmentMethod,
            first.CategoryId,
            first.ProductCreatedAt,
            group.Any(x => x.ProductVariantId.HasValue),
            group.Any(x => x.ProductSessionId.HasValue),
            MapPrice(group)
        );
    }

    public static PublicProductPriceSummaryV3Dto MapPrice(
        IEnumerable<PublicCatalogOfferCandidate> source
    )
    {
        var offers = source.ToArray();
        var selected = offers.OrderBy(x => x.FinalPriceMinor).ThenBy(x => x.OfferId).First();
        var min = offers.Min(x => x.FinalPriceMinor);
        var max = offers.Max(x => x.FinalPriceMinor);
        return new(
            min == max ? "Fixed" : "Range",
            min,
            max,
            offers.Length,
            selected.OfferId,
            selected.ShopId,
            selected.ShopSlug,
            selected.OriginalPriceMinor,
            selected.DiscountPercent,
            selected.FinalPriceMinor
        );
    }

    public static PublicOfferV3Dto MapOffer(PublicCatalogOfferCandidate x) =>
        new(
            x.OfferId,
            x.ProductId,
            x.ShopId,
            x.ShopName,
            x.ShopSlug,
            x.ProductVariantId,
            x.ProductSessionId,
            x.OriginalPriceMinor,
            x.DiscountPercent,
            x.FinalPriceMinor,
            x.StartDateUtc,
            x.EndDateUtc,
            x.OfferVersion,
            x.OfferUpdatedAt ?? x.StartDateUtc,
            true,
            "AVAILABLE",
            new(x.ShopId, x.ProductId, x.ProductVariantId, x.ProductSessionId)
        );
}
