using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Offers;
using Refahi.Modules.Store.Application.Contracts.Products.V3;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementCategoryTerms;

namespace Refahi.Modules.Store.Application.Features.CatalogV3;

internal static class CatalogV3Mapper
{
    public static ProductV3Dto Map(Product x, short eligibleSalesChannels = 0) =>
        new(
            x.Id,
            x.SupplierId,
            x.CategoryId,
            (short)x.ProductType,
            (short)x.SalesModel,
            (short)x.FulfillmentMethod,
            x.Title,
            x.Slug,
            x.Description,
            x.IsAvailable,
            x.IsDeleted,
            x.CreatedAt,
            x.UpdatedAt,
            x.Version
        )
        {
            EligibleSalesChannels = eligibleSalesChannels,
        };

    public static OfferDto Map(Offer x) =>
        new(
            x.Id,
            x.ProductId,
            x.ShopId,
            x.ProductVariantId,
            x.ProductSessionId,
            x.OriginalPriceMinor,
            x.DiscountPercent,
            x.FinalPriceMinor,
            x.StartDateUtc,
            x.EndDateUtc,
            x.IsActive,
            x.IsDeleted,
            x.CreatedAt,
            x.UpdatedAt,
            x.Version
        );

    public static ProductVariantV3Dto MapVariant(Product product, ProductVariant x, IPathService pathService) =>
        new(
            x.Id,
            x.SKU,
            x.ImageUrl is null ? null : pathService.MakeAbsoluteMediaUrl(x.ImageUrl),
            x.StockCount,
            x.FromDate,
            x.ToDate,
            x.CapacityType,
            x.Capacity,
            x.RequiresUsageDate,
            x.IsAvailable,
            x.Combinations.Select(c =>
                {
                    var attribute = product.VariantAttributes.First(a =>
                        a.Id == c.VariantAttributeId
                    );
                    var value = attribute.Values.First(v => v.Id == c.VariantAttributeValueId);
                    return new VariantCombinationDto(
                        attribute.Id,
                        attribute.Name,
                        value.Id,
                        value.Value
                    );
                })
                .ToArray()
        );

    public static ProductSessionV3Dto MapSession(ProductSession x) =>
        new(
            x.Id,
            x.Date.ToString("yyyy-MM-dd"),
            x.StartTime.ToString("HH:mm"),
            x.EndTime.ToString("HH:mm"),
            x.Title,
            x.Capacity,
            x.SoldCount,
            x.RemainingCapacity,
            x.IsActive,
            x.IsCancelled,
            x.IsAvailable
        );
}

internal static class CatalogEligibility
{
    public static async Task<
        IReadOnlyDictionary<(Guid SupplierId, int CategoryId), short>
    > ResolveChannelsAsync(
        IEnumerable<Product> products,
        IMediator mediator,
        DateTimeOffset atUtc,
        CancellationToken ct
    )
    {
        var coordinates = products.Select(x => (x.SupplierId, x.CategoryId)).Distinct().ToArray();
        var requests = coordinates
            .SelectMany(x =>
                new[]
                {
                    new AgreementCategoryTermResolutionRequest(
                        x.SupplierId,
                        x.CategoryId,
                        (short)SalesChannel.Online,
                        atUtc
                    ),
                    new AgreementCategoryTermResolutionRequest(
                        x.SupplierId,
                        x.CategoryId,
                        (short)SalesChannel.InPerson,
                        atUtc
                    ),
                }
            )
            .ToArray();
        var masks = new Dictionary<(Guid SupplierId, int CategoryId), short>();
        foreach (var chunk in requests.Chunk(1000))
        {
            var resolved = await mediator.Send(
                new ResolveAgreementCategoryTermsBatchQuery(chunk),
                ct
            );
            foreach (var hit in resolved.Where(x => x.Term is not null))
            {
                var key = (hit.Request.SupplierId, hit.Request.CategoryId);
                masks[key] = (short)(masks.GetValueOrDefault(key) | hit.Request.SalesChannel);
            }
        }
        return masks;
    }
}

internal static class CatalogAuthorization
{
    public static async Task DemandAsync(
        IMediator mediator,
        Guid actor,
        bool isAdmin,
        Guid supplierId,
        Guid? shopId,
        CancellationToken ct
    )
    {
        if (isAdmin)
            return;
        if (
            actor == Guid.Empty
            || !await mediator.Send(
                new AuthorizeStoreResourceQuery(
                    actor,
                    supplierId,
                    shopId,
                    StorePermissions.ManageCatalog
                ),
                ct
            )
        )
            throw new StoreDomainException(
                "دسترسی مدیریت کاتالوگ وجود ندارد",
                "CATALOG_ACCESS_DENIED"
            );
    }
}

public sealed class CreateProductV3Handler(IProductRepository products, IMediator mediator)
    : IRequestHandler<CreateProductV3Command, ProductV3Dto>
{
    public async Task<ProductV3Dto> Handle(CreateProductV3Command r, CancellationToken ct)
    {
        await CatalogAuthorization.DemandAsync(
            mediator,
            r.ActorUserId,
            r.IsAdmin,
            r.SupplierId,
            null,
            ct
        );
        var product = Product.CreateCatalogProduct(
            r.SupplierId,
            r.CategoryId,
            (ProductType)r.ProductType,
            (SalesModel)r.SalesModel,
            (FulfillmentMethod)r.FulfillmentMethod,
            r.Title,
            r.Slug,
            r.Description
        );
        var now = DateTimeOffset.UtcNow;
        var eligibility = await CatalogEligibility.ResolveChannelsAsync(
            [product],
            mediator,
            now,
            ct
        );
        if (
            !eligibility.TryGetValue((r.SupplierId, r.CategoryId), out var eligibleChannels)
            || eligibleChannels == 0
        )
            throw new StoreDomainException(
                "قرارداد معتبر برای دسته‌بندی محصول وجود ندارد",
                "AGREEMENT_TERM_NOT_FOUND"
            );
        if (await products.SlugExistsAsync(r.Slug.Trim().ToLowerInvariant(), ct))
            throw new StoreDomainException("این اسلاگ قبلاً ثبت شده است", "SLUG_ALREADY_EXISTS");
        await products.AddAsync(product, ct);
        return CatalogV3Mapper.Map(product, eligibleChannels);
    }
}

public sealed class UpdateProductV3Handler(IProductRepository products, IMediator mediator)
    : IRequestHandler<UpdateProductV3Command, ProductV3Dto>
{
    public async Task<ProductV3Dto> Handle(UpdateProductV3Command r, CancellationToken ct)
    {
        var product =
            await products.GetByIdAsync(r.ProductId, ct)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");
        await CatalogAuthorization.DemandAsync(
            mediator,
            r.ActorUserId,
            r.IsAdmin,
            product.SupplierId,
            null,
            ct
        );
        product.UpdateCatalogContent(r.Title, r.Description);
        await products.UpdateAsync(product, ct);
        var eligibility = await CatalogEligibility.ResolveChannelsAsync(
            [product],
            mediator,
            DateTimeOffset.UtcNow,
            ct
        );
        return CatalogV3Mapper.Map(
            product,
            eligibility.GetValueOrDefault((product.SupplierId, product.CategoryId))
        );
    }
}

public sealed class SetProductV3ActivationHandler(IProductRepository products, IMediator mediator)
    : IRequestHandler<SetProductV3ActivationCommand, ProductV3Dto>
{
    public async Task<ProductV3Dto> Handle(SetProductV3ActivationCommand r, CancellationToken ct)
    {
        var product =
            await products.GetByIdAsync(r.ProductId, ct)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");
        await CatalogAuthorization.DemandAsync(
            mediator,
            r.ActorUserId,
            r.IsAdmin,
            product.SupplierId,
            null,
            ct
        );
        var eligibility = await CatalogEligibility.ResolveChannelsAsync(
            [product],
            mediator,
            DateTimeOffset.UtcNow,
            ct
        );
        var eligibleChannels = eligibility.GetValueOrDefault(
            (product.SupplierId, product.CategoryId)
        );
        if (r.IsActive && eligibleChannels == 0)
            throw new StoreDomainException(
                "قرارداد معتبر برای فعال‌سازی محصول وجود ندارد",
                "AGREEMENT_TERM_NOT_FOUND"
            );
        if (r.IsActive)
            product.Activate();
        else
            product.Suspend();
        await products.UpdateAsync(product, ct);
        return CatalogV3Mapper.Map(product, eligibleChannels);
    }
}

public sealed class DeleteProductV3Handler(IProductRepository products, IMediator mediator)
    : IRequestHandler<DeleteProductV3Command, Unit>
{
    public async Task<Unit> Handle(DeleteProductV3Command r, CancellationToken ct)
    {
        var product =
            await products.GetByIdAsync(r.ProductId, ct)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");
        await CatalogAuthorization.DemandAsync(
            mediator,
            r.ActorUserId,
            r.IsAdmin,
            product.SupplierId,
            null,
            ct
        );
        product.SoftDelete();
        await products.UpdateAsync(product, ct);
        return Unit.Value;
    }
}

public sealed class GetProductV3Handler(IProductRepository products, IMediator mediator)
    : IRequestHandler<GetProductV3Query, ProductV3Dto?>
{
    public async Task<ProductV3Dto?> Handle(GetProductV3Query r, CancellationToken ct)
    {
        var x = r.IncludeInactive
            ? await products.GetByIdForAdminAsync(r.ProductId, ct)
            : await products.GetByIdAsync(r.ProductId, ct);
        if (x is null || (!r.IncludeInactive && !x.IsAvailable) || x.SupplierId == Guid.Empty)
            return null;
        var eligibility = await CatalogEligibility.ResolveChannelsAsync(
            [x],
            mediator,
            DateTimeOffset.UtcNow,
            ct
        );
        var eligibleChannels = eligibility.GetValueOrDefault((x.SupplierId, x.CategoryId));
        if (!r.IncludeInactive && (eligibleChannels & (short)SalesChannel.Online) == 0)
            return null;
        return CatalogV3Mapper.Map(x, eligibleChannels);
    }
}

public sealed class GetProductV3ManagementDetailHandler(IMediator mediator)
    : IRequestHandler<GetProductV3ManagementDetailQuery, ProductV3ManagementDetailDto?>
{
    public async Task<ProductV3ManagementDetailDto?> Handle(
        GetProductV3ManagementDetailQuery request,
        CancellationToken ct
    )
    {
        var product = await mediator.Send(new GetProductV3Query(request.ProductId, true), ct);
        if (product is null)
            return null;
        var detail = await mediator.Send(new AdminGetProductQuery(request.ProductId), ct);
        if (detail is null)
            return null;
        var variants = detail
            .Variants.Select(x => new ProductVariantV3Dto(
                x.Id,
                x.Sku,
                x.ImageUrl,
                x.StockCount,
                x.FromDate,
                x.ToDate,
                x.CapacityType,
                x.Capacity,
                x.RequiresUsageDate,
                x.IsAvailable,
                x.Combinations
            ))
            .ToArray();
        var sessions = (detail.Sessions ?? [])
            .Select(x => new ProductSessionV3Dto(
                x.Id,
                x.Date,
                x.StartTime,
                x.EndTime,
                x.Title,
                x.Capacity,
                x.SoldCount,
                x.RemainingCapacity,
                x.IsActive,
                x.IsCancelled,
                x.IsAvailable
            ))
            .ToArray();
        return new(
            product,
            detail.Images,
            variants,
            detail.VariantAttributes,
            detail.Specifications,
            sessions,
            "Offer",
            $"/api/store/v3/{request.Role}/products/{request.ProductId}"
        );
    }
}

public sealed class ProductV3SubresourceHandlers(
    IProductRepository products,
    IMediator mediator,
    IPathService pathService
) : IRequestHandler<CreateProductVariantV3Command, ProductVariantV3Dto>,
        IRequestHandler<UpdateProductVariantV3Command, ProductVariantV3Dto>,
        IRequestHandler<DeleteProductVariantV3Command, Unit>,
        IRequestHandler<CreateProductSessionV3Command, ProductSessionV3Dto>,
        IRequestHandler<UpdateProductSessionV3Command, ProductSessionV3Dto>
{
    private const long NeutralLegacyPrice = 1;
    private readonly IPathService _pathService = pathService;

    public async Task<ProductVariantV3Dto> Handle(
        CreateProductVariantV3Command r,
        CancellationToken ct
    )
    {
        var product = await GetV3Product(r.ProductId, ct);
        await CatalogAuthorization.DemandAsync(
            mediator,
            r.ActorUserId,
            r.IsAdmin,
            product.SupplierId,
            null,
            ct
        );
        var variant = product.AddVariant(
            r.Combinations.Select(x => (x.AttributeId, x.ValueId)).ToList(),
            r.StockCount,
            NeutralLegacyPrice,
            NeutralLegacyPrice,
            r.ImageUrl,
            r.Sku,
            r.FromDate,
            r.ToDate,
            r.CapacityType,
            r.Capacity,
            product.SalesModel
        );
        await products.AddProductVariantAsync(product, variant, ct);
        return CatalogV3Mapper.MapVariant(product, variant, _pathService);
    }

    public async Task<ProductVariantV3Dto> Handle(
        UpdateProductVariantV3Command r,
        CancellationToken ct
    )
    {
        var product = await GetV3Product(r.ProductId, ct);
        await CatalogAuthorization.DemandAsync(
            mediator,
            r.ActorUserId,
            r.IsAdmin,
            product.SupplierId,
            null,
            ct
        );
        product.UpdateVariant(
            r.VariantId,
            r.Combinations.Select(x => (x.AttributeId, x.ValueId)).ToList(),
            r.StockCount,
            NeutralLegacyPrice,
            NeutralLegacyPrice,
            r.ImageUrl,
            r.Sku,
            r.FromDate,
            r.ToDate,
            r.CapacityType,
            r.Capacity,
            product.SalesModel
        );
        await products.UpdateAsync(product, ct);
        return CatalogV3Mapper.MapVariant(
            product,
            product.Variants.Single(x => x.Id == r.VariantId),
            _pathService
        );
    }

    public async Task<Unit> Handle(DeleteProductVariantV3Command r, CancellationToken ct)
    {
        var product = await GetV3Product(r.ProductId, ct);
        await CatalogAuthorization.DemandAsync(
            mediator,
            r.ActorUserId,
            r.IsAdmin,
            product.SupplierId,
            null,
            ct
        );
        product.RemoveVariant(r.VariantId);
        await products.UpdateAsync(product, ct);
        return Unit.Value;
    }

    public async Task<ProductSessionV3Dto> Handle(
        CreateProductSessionV3Command r,
        CancellationToken ct
    )
    {
        var product = await GetV3Product(r.ProductId, ct);
        await CatalogAuthorization.DemandAsync(
            mediator,
            r.ActorUserId,
            r.IsAdmin,
            product.SupplierId,
            null,
            ct
        );
        EnsureSessionProduct(product);
        if (
            !DateOnly.TryParse(r.Date, out var date)
            || !TimeOnly.TryParse(r.StartTime, out var start)
            || !TimeOnly.TryParse(r.EndTime, out var end)
            || end <= start
        )
            throw new StoreDomainException(
                "تاریخ یا بازه زمانی سانس نامعتبر است",
                "INVALID_SESSION_TIME_RANGE"
            );
        product.AddSession(date, start, end, r.Capacity, r.Title, 0);
        await products.UpdateAsync(product, ct);
        return CatalogV3Mapper.MapSession(product.Sessions.Last());
    }

    public async Task<ProductSessionV3Dto> Handle(
        UpdateProductSessionV3Command r,
        CancellationToken ct
    )
    {
        var product = await GetV3Product(r.ProductId, ct);
        await CatalogAuthorization.DemandAsync(
            mediator,
            r.ActorUserId,
            r.IsAdmin,
            product.SupplierId,
            null,
            ct
        );
        EnsureSessionProduct(product);
        var session =
            product.Sessions.SingleOrDefault(x => x.Id == r.SessionId)
            ?? throw new StoreDomainException("سانس یافت نشد", "SESSION_NOT_FOUND");
        if (r.Capacity < session.SoldCount)
            throw new StoreDomainException(
                "ظرفیت سانس نمی‌تواند کمتر از تعداد فروخته‌شده باشد",
                "SESSION_CAPACITY_BELOW_SOLD_COUNT"
            );
        session.UpdateInfo(r.Capacity, r.Title, 0);
        if (r.IsActive)
            session.Activate();
        else
            session.Deactivate();
        await products.UpdateAsync(product, ct);
        return CatalogV3Mapper.MapSession(session);
    }

    private async Task<Product> GetV3Product(Guid id, CancellationToken ct)
    {
        var product =
            await products.GetByIdAsync(id, ct)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");
        if (product.AgreementProductId != Guid.Empty)
            throw new StoreDomainException(
                "این عملیات فقط برای محصول نسخه جدید مجاز است",
                "PRODUCT_NOT_V3"
            );
        return product;
    }

    private static void EnsureSessionProduct(Product product)
    {
        if (product.SalesModel != SalesModel.SessionBased)
            throw new StoreDomainException("این محصول سانسی نیست", "NOT_SESSION_PRODUCT");
    }
}

public sealed class ListProductsV3Handler(IProductRepository products, IMediator mediator)
    : IRequestHandler<ListProductsV3Query, ProductV3Page>
{
    public async Task<ProductV3Page> Handle(ListProductsV3Query r, CancellationToken ct)
    {
        if (r.IncludeInactive)
        {
            var (managementItems, managementTotal) = await products.GetCatalogPagedAsync(
                r.SupplierId,
                r.CategoryId,
                true,
                r.Page,
                r.PageSize,
                ct
            );
            var eligibility = await CatalogEligibility.ResolveChannelsAsync(
                managementItems,
                mediator,
                DateTimeOffset.UtcNow,
                ct
            );
            return new(
                managementItems
                    .Select(x =>
                        CatalogV3Mapper.Map(
                            x,
                            eligibility.GetValueOrDefault((x.SupplierId, x.CategoryId))
                        )
                    )
                    .ToArray(),
                managementTotal,
                r.Page,
                r.PageSize
            );
        }

        var candidates = await products.GetCatalogEligibilityCandidatesAsync(
            r.SupplierId,
            r.CategoryId,
            ct
        );
        var atUtc = DateTimeOffset.UtcNow;
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

        var eligibleIds = candidates
            .Where(x => allowed.Contains((x.SupplierId, x.CategoryId)))
            .Select(x => x.Id)
            .ToArray();
        var (items, total) = await products.GetCatalogPageByIdsAsync(
            eligibleIds,
            r.Page,
            r.PageSize,
            ct
        );
        return new(
            items.Select(x => CatalogV3Mapper.Map(x, (short)SalesChannel.Online)).ToArray(),
            total,
            r.Page,
            r.PageSize
        );
    }
}

public sealed class OfferCommandHandlers(
    IOfferRepository offers,
    IProductRepository products,
    IShopRepository shops,
    IMediator mediator,
    ILogger<OfferCommandHandlers> logger
)
    : IRequestHandler<CreateOfferCommand, OfferDto>,
        IRequestHandler<UpdateOfferCommand, OfferDto>,
        IRequestHandler<SetOfferActivationCommand, OfferDto>,
        IRequestHandler<DeleteOfferCommand, Unit>
{
    public async Task<OfferDto> Handle(CreateOfferCommand r, CancellationToken ct)
    {
        var (product, shop) = await ValidateCoordinate(
            r.ProductId,
            r.ShopId,
            r.ProductVariantId,
            r.ProductSessionId,
            ct
        );
        await CatalogAuthorization.DemandAsync(
            mediator,
            r.ActorUserId,
            r.IsAdmin,
            product.SupplierId,
            shop.Id,
            ct
        );
        if (
            !r.EndDateUtc.HasValue
            && await offers.HasOpenEndedCoordinateAsync(
                r.ProductId,
                r.ShopId,
                r.ProductVariantId,
                r.ProductSessionId,
                null,
                ct
            )
        )
            throw new StoreDomainException(
                "برای این مختصات یک پیشنهاد بدون پایان وجود دارد",
                "OPEN_OFFER_ALREADY_EXISTS"
            );
        var offer = Offer.Create(
            r.ProductId,
            r.ShopId,
            r.ProductVariantId,
            r.ProductSessionId,
            r.OriginalPriceMinor,
            r.DiscountPercent,
            r.StartDateUtc,
            r.EndDateUtc
        );
        await offers.AddAsync(offer, ct);
        logger.LogInformation(
            "Store offer created. OfferId={OfferId} ProductId={ProductId} ShopId={ShopId} SupplierId={SupplierId}",
            offer.Id,
            product.Id,
            shop.Id,
            product.SupplierId
        );
        return CatalogV3Mapper.Map(offer);
    }

    public async Task<OfferDto> Handle(UpdateOfferCommand r, CancellationToken ct)
    {
        var offer =
            await offers.GetByIdAsync(r.OfferId, false, ct)
            ?? throw new StoreDomainException("پیشنهاد یافت نشد", "OFFER_NOT_FOUND");
        var (product, shop) = await ValidateCoordinate(
            offer.ProductId,
            offer.ShopId,
            offer.ProductVariantId,
            offer.ProductSessionId,
            ct
        );
        await CatalogAuthorization.DemandAsync(
            mediator,
            r.ActorUserId,
            r.IsAdmin,
            product.SupplierId,
            shop.Id,
            ct
        );
        if (
            !r.EndDateUtc.HasValue
            && await offers.HasOpenEndedCoordinateAsync(
                offer.ProductId,
                offer.ShopId,
                offer.ProductVariantId,
                offer.ProductSessionId,
                offer.Id,
                ct
            )
        )
            throw new StoreDomainException(
                "برای این مختصات یک پیشنهاد بدون پایان وجود دارد",
                "OPEN_OFFER_ALREADY_EXISTS"
            );
        offer.Update(r.OriginalPriceMinor, r.DiscountPercent, r.StartDateUtc, r.EndDateUtc);
        await offers.UpdateAsync(offer, r.ExpectedVersion, ct);
        return CatalogV3Mapper.Map(offer);
    }

    public async Task<OfferDto> Handle(SetOfferActivationCommand r, CancellationToken ct)
    {
        var offer =
            await offers.GetByIdAsync(r.OfferId, false, ct)
            ?? throw new StoreDomainException("پیشنهاد یافت نشد", "OFFER_NOT_FOUND");
        var (product, shop) = await ValidateCoordinate(
            offer.ProductId,
            offer.ShopId,
            offer.ProductVariantId,
            offer.ProductSessionId,
            ct
        );
        await CatalogAuthorization.DemandAsync(
            mediator,
            r.ActorUserId,
            r.IsAdmin,
            product.SupplierId,
            shop.Id,
            ct
        );
        if (r.IsActive)
        {
            if (!product.IsAvailable || product.IsDeleted)
                throw new StoreDomainException("محصول فعال نیست", "PRODUCT_NOT_ACTIVE");
            if (shop.Status != ShopStatus.Active)
                throw new StoreDomainException("فروشگاه فعال نیست", "SHOP_NOT_ACTIVE");
            if (
                await mediator.Send(
                    new ResolveAgreementCategoryTermQuery(
                        product.SupplierId,
                        product.CategoryId,
                        (short)shop.Channel,
                        DateTimeOffset.UtcNow
                    ),
                    ct
                )
                is null
            )
                throw new StoreDomainException(
                    "قرارداد معتبر برای فعال‌سازی پیشنهاد وجود ندارد",
                    "AGREEMENT_TERM_NOT_FOUND"
                );
            offer.Activate();
        }
        else
            offer.Deactivate();
        await offers.UpdateAsync(offer, r.ExpectedVersion, ct);
        logger.LogInformation(
            "Store offer activation changed. OfferId={OfferId} ProductId={ProductId} ShopId={ShopId} IsActive={IsActive}",
            offer.Id,
            offer.ProductId,
            offer.ShopId,
            offer.IsActive
        );
        return CatalogV3Mapper.Map(offer);
    }

    public async Task<Unit> Handle(DeleteOfferCommand r, CancellationToken ct)
    {
        var offer =
            await offers.GetByIdAsync(r.OfferId, false, ct)
            ?? throw new StoreDomainException("پیشنهاد یافت نشد", "OFFER_NOT_FOUND");
        var (product, shop) = await ValidateCoordinate(
            offer.ProductId,
            offer.ShopId,
            offer.ProductVariantId,
            offer.ProductSessionId,
            ct
        );
        await CatalogAuthorization.DemandAsync(
            mediator,
            r.ActorUserId,
            r.IsAdmin,
            product.SupplierId,
            shop.Id,
            ct
        );
        offer.SoftDelete();
        await offers.UpdateAsync(offer, r.ExpectedVersion, ct);
        return Unit.Value;
    }

    private async Task<(Product, Shop)> ValidateCoordinate(
        Guid productId,
        Guid shopId,
        Guid? variantId,
        Guid? sessionId,
        CancellationToken ct
    )
    {
        var product =
            await products.GetByIdAsync(productId, ct)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");
        var shop =
            await shops.GetByIdAsync(shopId, ct)
            ?? throw new StoreDomainException("فروشگاه یافت نشد", "SHOP_NOT_FOUND");
        if (product.SupplierId == Guid.Empty || product.SupplierId != shop.SupplierId)
            throw new StoreDomainException(
                "محصول و فروشگاه متعلق به یک تامین‌کننده نیستند",
                "SUPPLIER_MISMATCH"
            );
        if (variantId.HasValue && !product.Variants.Any(x => x.Id == variantId))
            throw new StoreDomainException("تنوع متعلق به محصول نیست", "VARIANT_PRODUCT_MISMATCH");
        if (sessionId.HasValue && !product.Sessions.Any(x => x.Id == sessionId))
            throw new StoreDomainException("سانس متعلق به محصول نیست", "SESSION_PRODUCT_MISMATCH");
        return (product, shop);
    }
}

public sealed class OfferQueryHandlers(
    IOfferRepository offers,
    IProductRepository products,
    IShopRepository shops,
    IMediator mediator
)
    : IRequestHandler<GetOfferQuery, OfferDto?>,
        IRequestHandler<ListOffersQuery, OfferPage>,
        IRequestHandler<ResolveOfferQuery, OfferDto?>
{
    public async Task<OfferDto?> Handle(GetOfferQuery r, CancellationToken ct)
    {
        var x = await offers.GetByIdAsync(r.OfferId, r.IncludeDeleted, ct);
        if (x is null)
            return null;
        if (r.EffectiveAtUtc.HasValue)
        {
            if (!x.IsEffectiveAt(r.EffectiveAtUtc.Value))
                return null;
            var product = await products.GetByIdAsync(x.ProductId, ct);
            var shop = await shops.GetByIdAsync(x.ShopId, ct);
            if (
                product is null
                || shop is null
                || shop.Status != ShopStatus.Active
                || product.SupplierId != shop.SupplierId
                || await mediator.Send(
                    new ResolveAgreementCategoryTermQuery(
                        product.SupplierId,
                        product.CategoryId,
                        (short)shop.Channel,
                        r.EffectiveAtUtc.Value
                    ),
                    ct
                )
                    is null
            )
                return null;
        }
        return CatalogV3Mapper.Map(x);
    }

    public async Task<OfferPage> Handle(ListOffersQuery r, CancellationToken ct)
    {
        if (!r.EffectiveAtUtc.HasValue)
        {
            var (managementItems, managementTotal) = await offers.GetPagedAsync(
                r.ProductId,
                r.ShopId,
                r.IncludeDeleted,
                null,
                r.Page,
                r.PageSize,
                ct
            );
            return new(
                managementItems.Select(CatalogV3Mapper.Map).ToArray(),
                managementTotal,
                r.Page,
                r.PageSize
            );
        }

        var atUtc = r.EffectiveAtUtc.Value;
        var candidates = await offers.GetEligibilityCandidatesAsync(
            r.ProductId,
            r.ShopId,
            atUtc,
            ct
        );
        var requests = candidates
            .Select(x => new AgreementCategoryTermResolutionRequest(
                x.SupplierId,
                x.CategoryId,
                x.SalesChannel,
                atUtc
            ))
            .Distinct()
            .ToArray();
        var allowed = new HashSet<(Guid SupplierId, int CategoryId, short SalesChannel)>();
        foreach (var chunk in requests.Chunk(1000))
        {
            var resolved = await mediator.Send(
                new ResolveAgreementCategoryTermsBatchQuery(chunk),
                ct
            );
            foreach (var hit in resolved.Where(x => x.Term is not null))
                allowed.Add(
                    (hit.Request.SupplierId, hit.Request.CategoryId, hit.Request.SalesChannel)
                );
        }

        var eligibleIds = candidates
            .Where(x => allowed.Contains((x.SupplierId, x.CategoryId, x.SalesChannel)))
            .Select(x => x.OfferId)
            .ToArray();
        var (items, total) = await offers.GetPageByIdsAsync(eligibleIds, r.Page, r.PageSize, ct);
        return new(items.Select(CatalogV3Mapper.Map).ToArray(), total, r.Page, r.PageSize);
    }

    public async Task<OfferDto?> Handle(ResolveOfferQuery r, CancellationToken ct) =>
        await offers.ResolveAsync(
            r.ProductId,
            r.ShopId,
            r.ProductVariantId,
            r.ProductSessionId,
            r.AtUtc,
            ct
        )
            is { } x
            ? CatalogV3Mapper.Map(x)
            : null;
}
