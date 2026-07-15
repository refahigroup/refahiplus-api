using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Store.Application.Contracts.Queries.Products.V2;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Products.GetSyntheticOffersV2;

public sealed class GetSyntheticOffersV2QueryHandler
    : IRequestHandler<GetSyntheticOffersV2Query, SyntheticOffersV2PagedResponse?>
{
    private readonly ISyntheticOfferQueryContextService _contextService;
    private readonly ISyntheticOfferReadRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly IStoreBusinessClock _clock;
    private readonly IPathService _pathService;
    private readonly ILogger<GetSyntheticOffersV2QueryHandler> _logger;

    public GetSyntheticOffersV2QueryHandler(
        ISyntheticOfferQueryContextService contextService,
        ISyntheticOfferReadRepository repository,
        IProductRepository productRepository,
        IStoreBusinessClock clock,
        IPathService pathService,
        ILogger<GetSyntheticOffersV2QueryHandler> logger)
    {
        _contextService = contextService;
        _repository = repository;
        _productRepository = productRepository;
        _clock = clock;
        _pathService = pathService;
        _logger = logger;
    }

    public async Task<SyntheticOffersV2PagedResponse?> Handle(GetSyntheticOffersV2Query request, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var context = await _contextService.ResolveAsync(
            request.ModuleId, request.CategoryId, request.ShopId, request.ShopSlug, request.SalesModel, ct);
        if (!context.IsShopValid)
            return null;

        Guid? productId = request.ProductId;
        if (!string.IsNullOrWhiteSpace(request.ProductSlug))
        {
            var product = await _productRepository.GetDisplayableBySlugAsync(
                request.ProductSlug.Trim().ToLowerInvariant(),
                context.ModuleAgreementProductIds.ToArray(),
                ct);
            if (product is null)
                return null;

            productId = product.Id;
        }
        else if (productId.HasValue)
        {
            var product = await _productRepository.GetByIdAsync(productId.Value, ct);
            if (product is null
                || product.IsDeleted
                || !product.IsAvailable
                || !context.ModuleAgreementProductIds.Contains(product.AgreementProductId))
            {
                return null;
            }
        }

        var now = _clock.Current;
        var spec = new SyntheticOfferQuerySpec(
            context.StockBasedAgreementProductIds,
            context.SessionBasedAgreementProductIds,
            now.Date,
            request.SearchQuery?.Trim(),
            context.ShopId,
            productId,
            null,
            NormalizeOfferKind(request.OfferKind),
            request.UsageFrom,
            request.UsageTo,
            request.MinPriceMinor,
            request.MaxPriceMinor,
            request.Sort.ToLowerInvariant(),
            request.PageNumber,
            request.PageSize,
            now.Time);

        var (rows, total) = await _repository.GetOffersAsync(spec, ct);
        var items = rows
            .Where(x => context.AgreementProducts.ContainsKey(x.AgreementProductId))
            .Select(x => SyntheticOfferDtoMapper.MapOffer(
                x, context.AgreementProducts[x.AgreementProductId], _pathService))
            .ToList();

        stopwatch.Stop();
        _logger.LogInformation(
            "Store synthetic offers query completed. ModuleId={ModuleId} Page={Page} PageSize={PageSize} Total={Total} Returned={Returned} HasSearch={HasSearch} DurationMs={DurationMs}",
            request.ModuleId, request.PageNumber, request.PageSize, total, items.Count,
            !string.IsNullOrWhiteSpace(request.SearchQuery), stopwatch.ElapsedMilliseconds);

        return new SyntheticOffersV2PagedResponse(
            items,
            request.PageNumber,
            request.PageSize,
            total,
            (int)Math.Ceiling(total / (double)request.PageSize));
    }

    private static string? NormalizeOfferKind(string? value)
        => value?.Trim() switch
        {
            { } x when x.Equals("StockProduct", StringComparison.OrdinalIgnoreCase) => "StockProduct",
            { } x when x.Equals("StockVariant", StringComparison.OrdinalIgnoreCase) => "StockVariant",
            { } x when x.Equals("ProductSession", StringComparison.OrdinalIgnoreCase) => "ProductSession",
            { } x when x.Equals("SessionVariant", StringComparison.OrdinalIgnoreCase) => "SessionVariant",
            _ => null
        };
}
