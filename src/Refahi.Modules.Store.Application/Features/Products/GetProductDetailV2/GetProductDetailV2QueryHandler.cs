using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products.V2;
using Refahi.Modules.Store.Application.Contracts.Queries.Products.V2;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Products.GetProductDetailV2;

public sealed class GetProductDetailV2QueryHandler
    : IRequestHandler<GetProductDetailV2Query, ProductDetailV2Dto?>
{
    private readonly ISyntheticOfferQueryContextService _contextService;
    private readonly ISyntheticOfferReadRepository _offerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IReviewRepository _reviewRepository;
    private readonly IStoreBusinessClock _clock;
    private readonly IPathService _pathService;
    private readonly ILogger<GetProductDetailV2QueryHandler> _logger;

    public GetProductDetailV2QueryHandler(
        ISyntheticOfferQueryContextService contextService,
        ISyntheticOfferReadRepository offerRepository,
        IProductRepository productRepository,
        IReviewRepository reviewRepository,
        IStoreBusinessClock clock,
        IPathService pathService,
        ILogger<GetProductDetailV2QueryHandler> logger)
    {
        _contextService = contextService;
        _offerRepository = offerRepository;
        _productRepository = productRepository;
        _reviewRepository = reviewRepository;
        _clock = clock;
        _pathService = pathService;
        _logger = logger;
    }

    public async Task<ProductDetailV2Dto?> Handle(GetProductDetailV2Query request, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var context = await _contextService.ResolveAsync(
            request.ModuleId, null, request.ShopId, request.ShopSlug, null, ct);
        if (!context.IsShopValid || context.AgreementProducts.Count == 0)
            return null;

        var product = await _productRepository.GetDisplayableBySlugAsync(
            request.Slug.Trim().ToLowerInvariant(),
            context.AgreementProducts.Keys.ToArray(),
            ct);
        if (product is null || !context.AgreementProducts.TryGetValue(product.AgreementProductId, out var ap))
            return null;

        var now = _clock.Current;
        var allOffers = await _offerRepository.GetProductOffersAsync(
            new SyntheticOfferQuerySpec(
                context.StockBasedAgreementProductIds,
                context.SessionBasedAgreementProductIds,
                now.Date,
                ShopId: context.ShopId,
                ProductId: product.Id,
                ProductSlug: product.Slug,
                CurrentTime: now.Time),
            ct);
        if (allOffers.Count == 0)
            return null;

        var preferred = allOffers.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(request.OfferKey)
                && x.OfferKey.Equals(request.OfferKey.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? allOffers
                .Where(x => request.VariantId.HasValue && x.VariantId == request.VariantId)
                .OrderBy(x => x.EffectivePriceMinor)
                .ThenBy(x => x.OfferKey, StringComparer.Ordinal)
                .FirstOrDefault()
            ?? allOffers
                .OrderBy(x => x.EffectivePriceMinor)
                .ThenBy(x => x.OfferKey, StringComparer.Ordinal)
                .First();

        var selectedOffers = allOffers
            .Where(x => x.ShopId == preferred.ShopId)
            .OrderBy(x => x.SessionDate ?? x.FromDate ?? DateOnly.MaxValue)
            .ThenBy(x => x.SessionStartTime ?? TimeOnly.MaxValue)
            .ThenBy(x => x.EffectivePriceMinor)
            .ThenBy(x => x.OfferKey, StringComparer.Ordinal)
            .ToList();

        var defaultOffer = selectedOffers.FirstOrDefault(x => x.OfferKey == preferred.OfferKey) ?? selectedOffers[0];
        var offers = selectedOffers
            .Select(x => SyntheticOfferDtoMapper.MapOffer(x, ap, _pathService))
            .ToList();
        var averageRating = await _reviewRepository.GetAverageRatingAsync(product.Id, ct);
        var (_, reviewCount) = await _reviewRepository.GetPagedAsync(product.Id, true, 1, 1, ct);

        stopwatch.Stop();
        _logger.LogInformation(
            "Store synthetic product detail query completed. ModuleId={ModuleId} ProductId={ProductId} ShopId={ShopId} Offers={Offers} DurationMs={DurationMs}",
            request.ModuleId, product.Id, preferred.ShopId, offers.Count, stopwatch.ElapsedMilliseconds);

        return new ProductDetailV2Dto(
            product.Id,
            product.Title,
            product.Slug,
            product.Description,
            ((ProductType)ap.ProductType).ToString(),
            ((DeliveryType)ap.DeliveryType).ToString(),
            ((SalesModel)ap.SalesModel).ToString(),
            ap.CategoryId,
            ap.CategoryName,
            product.IsAvailable,
            selectedOffers.Min(x => x.EffectivePriceMinor) == selectedOffers.Max(x => x.EffectivePriceMinor) ? "Exact" : "Range",
            selectedOffers.Min(x => x.EffectivePriceMinor),
            selectedOffers.Max(x => x.EffectivePriceMinor),
            defaultOffer.OfferKey,
            new SelectedShopV2Dto(preferred.ShopId, preferred.ShopName, preferred.ShopSlug),
            product.Images.OrderBy(x => x.SortOrder)
                .Select(x => new ProductImageDto(x.Id, _pathService.MakeAbsoluteMediaUrl(x.ImageUrl), x.IsMain, x.SortOrder))
                .ToList(),
            product.Specifications.OrderBy(x => x.SortOrder)
                .Select(x => new ProductSpecificationDto(x.Id, x.Key, x.Value, x.SortOrder))
                .ToList(),
            offers,
            averageRating,
            reviewCount,
            product.CreatedAt);
    }
}
