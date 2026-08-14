using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Products;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementCategoryTerms;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Catalog;

public sealed class GetPublicProductCatalogHandler(
    IPublicCatalogRepository catalog,
    IStoreModuleRepository modules,
    IMediator mediator,
    IPathService pathService
) : IRequestHandler<GetPublicProductCatalogQuery, PublicProductCatalogPage?>
{
    private readonly IPathService _pathService = pathService;
    public async Task<PublicProductCatalogPage?> Handle(
        GetPublicProductCatalogQuery request,
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

        if (!module.CategoryId.HasValue)
            throw new StoreDomainException("دسته‌بندی ریشه ماژول تنظیم نشده است", "MODULE_CATEGORY_REQUIRED");
        var moduleCategoryIds = await mediator.Send(new GetCategorySubtreeIdsQuery(module.CategoryId.Value), ct);
        IReadOnlyList<int> scopedCategoryIds = moduleCategoryIds;
        if (request.CategoryId.HasValue)
        {
            if (!moduleCategoryIds.Contains(request.CategoryId.Value))
                throw new StoreDomainException("دسته‌بندی خارج از محدوده ماژول است", "CATEGORY_OUTSIDE_MODULE");
            var requestedSubtree = await mediator.Send(
                new GetCategorySubtreeIdsQuery(request.CategoryId.Value), ct);
            scopedCategoryIds = requestedSubtree.Where(moduleCategoryIds.Contains).ToArray();
        }

        var atUtc = DateTimeOffset.UtcNow;
        var coordinates = await catalog.GetEligibilityCoordinatesAsync(
            scopedCategoryIds,
            request.ShopId,
            request.ShopSlug,
            request.Search,
            salesModel,
            atUtc,
            ct
        );
        var allowed = await PublicCatalogEligibility.ResolveAsync(coordinates, mediator, atUtc, ct);
        var page = await catalog.GetEffectivePageAsync(
            scopedCategoryIds, allowed, request.ShopId, request.ShopSlug, request.Search, salesModel,
            request.MinPriceMinor, request.MaxPriceMinor, NormalizeSort(request.Sort),
            request.Page, request.PageSize, atUtc, ct);
        var items = page.Candidates
            .GroupBy(x => x.ProductId)
            .Select(x => PublicCatalogMapping.MapGroup(x, _pathService))
            .ToArray();
        return new(
            items,
            page.Total,
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

public sealed class GetPublicProductDetailHandler(
    IPublicCatalogRepository catalog,
    IStoreModuleRepository modules,
    IProductRepository products,
    IMediator mediator,
    IPathService pathService,
    ILogger<GetPublicProductDetailHandler> logger
) : IRequestHandler<GetPublicProductDetailQuery, PublicProductDetailDto?>
{
    private readonly IPathService _pathService = pathService;
    public async Task<PublicProductDetailDto?> Handle(
        GetPublicProductDetailQuery request,
        CancellationToken ct
    )
    {
        if (
            string.IsNullOrWhiteSpace(request.ModuleSlug)
            || string.IsNullOrWhiteSpace(request.ProductSlug)
        )
        {
            throw new StoreDomainException(
                "اسلاگ ماژول و محصول الزامی است",
                "SLUG_REQUIRED"
            );
        }

        var normalizedModuleSlug =
            request.ModuleSlug.Trim().ToLowerInvariant();

        var normalizedProductSlug =
            request.ProductSlug.Trim().ToLowerInvariant();

        var module = await modules.GetBySlugAsync(
            normalizedModuleSlug,
            ct
        );

        if (module is null || !module.IsActive)
            return null;

        var atUtc = DateTimeOffset.UtcNow;
        if (!module.CategoryId.HasValue)
            return null;
        var moduleCategoryIds = await mediator.Send(new GetCategorySubtreeIdsQuery(module.CategoryId.Value), ct);

        /*
         * مهم:
         * در نسخه قبلی ProductSlug اینجا به Repository ارسال نمی‌شد.
         *
         * در نتیجه تمام Offerهای Category/Module از DB گرفته می‌شدند،
         * Eligibility روی همه آنها اجرا می‌شد و تازه بعد از آن
         * ProductSlug در حافظه فیلتر می‌شد.
         *
         * حالا ProductSlug مستقیماً وارد Query دیتابیس می‌شود.
         */
        var candidates = (await catalog.GetEffectiveCandidatesAsync(
            moduleCategoryId: null,
            categoryId: null,
            shopId: request.ShopId,
            shopSlug: request.ShopSlug,
            productSlug: normalizedProductSlug,
            search: null,
            salesModel: null,
            atUtc: atUtc,
            ct: ct
        )).Where(x => moduleCategoryIds.Contains(x.CategoryId)).ToArray();

        /*
         * candidates در این مرحله فقط متعلق به همین Product است.
         * بنابراین دیگر Where(ProductSlug == ...) در Memory لازم نیست.
         */
        var eligible = (
            await PublicCatalogEligibility.FilterAsync(
                candidates,
                mediator,
                atUtc,
                ct
            )
        ).ToArray();

        if (eligible.Length == 0)
            return null;

        IEnumerable<PublicCatalogOfferCandidate> selectedCandidates =
            eligible;

        if (request.OfferId.HasValue)
        {
            selectedCandidates = selectedCandidates.Where(x =>
                x.OfferId == request.OfferId.Value
            );
        }

        if (request.VariantId.HasValue)
        {
            selectedCandidates = selectedCandidates.Where(x =>
                x.ProductVariantId == request.VariantId.Value
            );
        }

        if (request.SessionId.HasValue)
        {
            selectedCandidates = selectedCandidates.Where(x =>
                x.ProductSessionId == request.SessionId.Value
            );
        }

        var selected = selectedCandidates
            .OrderBy(x => x.FinalPriceMinor)
            .ThenBy(x => x.OfferId)
            .FirstOrDefault();

        if (
            (
                request.OfferId.HasValue
                || request.VariantId.HasValue
                || request.SessionId.HasValue
            )
            && selected is null
        )
        {
            return null;
        }

        selected ??= eligible
            .OrderBy(x => x.FinalPriceMinor)
            .ThenBy(x => x.OfferId)
            .First();

        logger?.LogWarning(
            """
    BEFORE GetByIdAsync
    ProductId: {ProductId}
    CancellationRequested: {CancellationRequested}
    CanBeCanceled: {CanBeCanceled}
    CandidateCount: {CandidateCount}
    EligibleCount: {EligibleCount}
    """,
            selected.ProductId,
            ct.IsCancellationRequested,
            ct.CanBeCanceled,
            candidates.Length,
            eligible.Length
        );

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Product? product = null;

        try
        {
            product = await products.GetByIdAsync(
                selected.ProductId,
                ct
            );

            logger?.LogWarning(
                """
        AFTER GetByIdAsync
        ProductId: {ProductId}
        ElapsedMs: {ElapsedMs}
        CancellationRequested: {CancellationRequested}
        Found: {Found}
        """,
                selected.ProductId,
                sw.ElapsedMilliseconds,
                ct.IsCancellationRequested,
                product is not null
            );

            if (product is null || product.SupplierId == Guid.Empty)
                return null;

        }
        catch (OperationCanceledException ex)
        {
            logger.LogError(
                ex,
                """
        GetByIdAsync CANCELED
        ProductId: {ProductId}
        ElapsedMs: {ElapsedMs}
        CancellationRequested: {CancellationRequested}
        """,
                selected.ProductId,
                sw.ElapsedMilliseconds,
                ct.IsCancellationRequested
            );

            throw;
        }




        //var product = await products.GetByIdAsync(
        //    selected.ProductId,
        //    ct
        //);

        if (product is null || product.SupplierId == Guid.Empty)
            return null;

        var offers = eligible
            .OrderBy(x => x.FinalPriceMinor)
            .ThenBy(x => x.ShopSlug)
            .ThenBy(x => x.OfferId)
            .Select(PublicCatalogMapping.MapOffer)
            .ToArray();

        var summary = PublicCatalogMapping.MapPrice(eligible);

        var dto = CatalogMapper.Map(
            product,
            (short)SalesChannel.Online
        );

        return new(
            dto,
            product
                .Images
                .OrderBy(x => x.SortOrder)
                .Select(x => new ProductImageDto(
                    x.Id,
                    _pathService.MakeAbsoluteMediaUrl(x.ImageUrl),
                    x.IsMain,
                    x.SortOrder
                ))
                .ToArray(),
            product
                .Specifications
                .OrderBy(x => x.SortOrder)
                .Select(x => new ProductSpecificationDto(
                    x.Id,
                    x.Key,
                    x.Value,
                    x.SortOrder
                ))
                .ToArray(),
            product
                .VariantAttributes
                .OrderBy(x => x.SortOrder)
                .Select(x => new VariantAttributeDto(
                    x.Id,
                    x.Name,
                    x.SortOrder,
                    x.Values
                        .OrderBy(v => v.SortOrder)
                        .Select(v => new VariantAttributeValueDto(
                            v.Id,
                            v.Value,
                            v.SortOrder
                        ))
                        .ToList()
                ))
                .ToArray(),
            product
                .Variants
                .Select(x =>
                    CatalogMapper.MapVariant(
                        product,
                        x,
                        _pathService
                    )
                )
                .ToArray(),
            product
                .Sessions
                .Select(CatalogMapper.MapSession)
                .ToArray(),
            offers,
            summary,
            selected.OfferId,
            "Offer"
        );
    }
}

//internal static class PublicCatalogEligibility
//{
//    public static async Task<IReadOnlyList<PublicCatalogOfferCandidate>> FilterAsync(
//        IReadOnlyList<PublicCatalogOfferCandidate> candidates,
//        IMediator mediator,
//        DateTimeOffset atUtc,
//        CancellationToken ct
//    )
//    {
//        if (candidates.Count == 0)
//            return [];
//        var requests = candidates
//            .Select(x => new AgreementCategoryTermResolutionRequest(
//                x.SupplierId,
//                x.CategoryId,
//                (short)SalesChannel.Online,
//                atUtc
//            ))
//            .Distinct()
//            .ToArray();
//        var allowed = new HashSet<(Guid SupplierId, int CategoryId)>();
//        foreach (var chunk in requests.Chunk(1000))
//        {
//            var resolved = await mediator.Send(
//                new ResolveAgreementCategoryTermsBatchQuery(chunk),
//                ct
//            );
//            foreach (var hit in resolved.Where(x => x.Term is not null))
//                allowed.Add((hit.Request.SupplierId, hit.Request.CategoryId));
//        }
//        return candidates.Where(x => allowed.Contains((x.SupplierId, x.CategoryId))).ToArray();
//    }
//}

internal static class PublicCatalogEligibility
{
    public static async Task<IReadOnlyList<PublicCatalogEligibilityCoordinate>> ResolveAsync(
        IReadOnlyList<PublicCatalogEligibilityCoordinate> coordinates,
        IMediator mediator,
        DateTimeOffset atUtc,
        CancellationToken ct)
    {
        if (coordinates.Count == 0)
            return [];
        var requests = coordinates.Select(x => new AgreementCategoryTermResolutionRequest(
            x.SupplierId, x.CategoryId, (short)SalesChannel.Online, atUtc)).Distinct().ToArray();
        var allowed = new HashSet<(Guid SupplierId, int CategoryId)>();
        foreach (var chunk in requests.Chunk(1000))
        {
            var resolved = await mediator.Send(new ResolveAgreementCategoryTermsBatchQuery(chunk), ct);
            foreach (var hit in resolved.Where(x => x.Term is not null))
                allowed.Add((hit.Request.SupplierId, hit.Request.CategoryId));
        }
        return coordinates.Where(x => allowed.Contains((x.SupplierId, x.CategoryId))).ToArray();
    }

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
            {
                allowed.Add((
                    hit.Request.SupplierId,
                    hit.Request.CategoryId
                ));
            }
        }

        return candidates
            .Where(x =>
                allowed.Contains((
                    x.SupplierId,
                    x.CategoryId
                ))
            )
            .ToArray();
    }
}

internal static class PublicCatalogMapping
{
    public static PublicProductCatalogItemDto MapGroup(
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

    public static PublicProductPriceSummaryDto MapPrice(
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

    public static PublicOfferDto MapOffer(PublicCatalogOfferCandidate x) =>
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
