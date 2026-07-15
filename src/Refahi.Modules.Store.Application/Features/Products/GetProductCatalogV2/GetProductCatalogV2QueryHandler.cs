using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products.V2;
using Refahi.Modules.Store.Application.Contracts.Queries.Products.V2;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Products.GetProductCatalogV2;

public sealed class GetProductCatalogV2QueryHandler
    : IRequestHandler<GetProductCatalogV2Query, ProductCatalogV2PagedResponse?>
{
    private readonly ISyntheticOfferQueryContextService _contextService;
    private readonly ISyntheticOfferReadRepository _repository;
    private readonly IStoreBusinessClock _clock;
    private readonly IPathService _pathService;
    private readonly ILogger<GetProductCatalogV2QueryHandler> _logger;

    public GetProductCatalogV2QueryHandler(
        ISyntheticOfferQueryContextService contextService,
        ISyntheticOfferReadRepository repository,
        IStoreBusinessClock clock,
        IPathService pathService,
        ILogger<GetProductCatalogV2QueryHandler> logger)
    {
        _contextService = contextService;
        _repository = repository;
        _clock = clock;
        _pathService = pathService;
        _logger = logger;
    }

    public async Task<ProductCatalogV2PagedResponse?> Handle(GetProductCatalogV2Query request, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var context = await _contextService.ResolveAsync(
            request.ModuleId, request.CategoryId, request.ShopId, request.ShopSlug, request.SalesModel, ct);
        if (!context.IsShopValid)
            return null;

        var now = _clock.Current;
        var spec = new SyntheticOfferQuerySpec(
            context.StockBasedAgreementProductIds,
            context.SessionBasedAgreementProductIds,
            now.Date,
            request.SearchQuery?.Trim(),
            context.ShopId,
            MinPriceMinor: request.MinPriceMinor,
            MaxPriceMinor: request.MaxPriceMinor,
            Sort: request.Sort.ToLowerInvariant(),
            PageNumber: request.PageNumber,
            PageSize: request.PageSize,
            CurrentTime: now.Time);

        var (rows, total) = await _repository.GetProductCatalogAsync(spec, ct);
        var items = rows
            .Where(x => context.AgreementProducts.ContainsKey(x.AgreementProductId))
            .Select(x =>
            {
                var ap = context.AgreementProducts[x.AgreementProductId];
                return new ProductCatalogItemV2Dto(
                    x.ProductId,
                    x.ProductTitle,
                    x.ProductSlug,
                    x.ImageUrl is null ? null : _pathService.MakeAbsoluteMediaUrl(x.ImageUrl),
                    ((ProductType)ap.ProductType).ToString(),
                    ((DeliveryType)ap.DeliveryType).ToString(),
                    ((SalesModel)ap.SalesModel).ToString(),
                    ap.CategoryId,
                    ap.CategoryName,
                    x.MinEffectivePriceMinor == x.MaxEffectivePriceMinor ? "Exact" : "Range",
                    x.MinEffectivePriceMinor,
                    x.MaxEffectivePriceMinor,
                    x.DefaultOriginalPriceMinor,
                    x.DefaultDiscountedPriceMinor,
                    x.OfferCount,
                    x.HasVariants,
                    x.HasSessions,
                    x.DefaultOfferKey,
                    x.DefaultShopId,
                    x.DefaultShopSlug,
                    x.ProductCreatedAt);
            })
            .ToList();

        stopwatch.Stop();
        _logger.LogInformation(
            "Store synthetic product catalog query completed. ModuleId={ModuleId} Page={Page} PageSize={PageSize} Total={Total} Returned={Returned} HasSearch={HasSearch} DurationMs={DurationMs}",
            request.ModuleId, request.PageNumber, request.PageSize, total, items.Count,
            !string.IsNullOrWhiteSpace(request.SearchQuery), stopwatch.ElapsedMilliseconds);

        return new ProductCatalogV2PagedResponse(
            items,
            request.PageNumber,
            request.PageSize,
            total,
            (int)Math.Ceiling(total / (double)request.PageSize));
    }
}
