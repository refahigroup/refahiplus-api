using MediatR;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.Store.Application.Services;

public interface ISyntheticOfferQueryContextService
{
    Task<SyntheticOfferQueryContext> ResolveAsync(
        int moduleId,
        int? categoryId,
        Guid? shopId,
        string? shopSlug,
        string? salesModel,
        CancellationToken ct);
}

internal sealed class SyntheticOfferQueryContextService : ISyntheticOfferQueryContextService
{
    private readonly IStoreModuleCatalogService _catalog;
    private readonly IShopRepository _shopRepository;
    private readonly IMediator _mediator;

    public SyntheticOfferQueryContextService(
        IStoreModuleCatalogService catalog,
        IShopRepository shopRepository,
        IMediator mediator)
    {
        _catalog = catalog;
        _shopRepository = shopRepository;
        _mediator = mediator;
    }

    public async Task<SyntheticOfferQueryContext> ResolveAsync(
        int moduleId,
        int? categoryId,
        Guid? shopId,
        string? shopSlug,
        string? salesModel,
        CancellationToken ct)
    {
        var resolvedShop = await ResolveShopAsync(shopId, shopSlug, ct);
        if (!resolvedShop.IsValid)
            return SyntheticOfferQueryContext.InvalidShop;

        var allowedIds = await _catalog.GetDisplayableAgreementProductIdsAsync(moduleId, ct);
        if (allowedIds.Count == 0)
            return SyntheticOfferQueryContext.Empty(resolvedShop.ShopId);

        var agreementProducts = await _mediator.Send(new GetAgreementProductsByIdsQuery(allowedIds), ct);
        var moduleAgreementProducts = agreementProducts.Values.Where(x => !x.IsDeleted).ToList();
        IEnumerable<AgreementProductDto> filtered = moduleAgreementProducts;

        if (categoryId.HasValue)
        {
            var categoryIds = await _mediator.Send(new GetCategorySubtreeIdsQuery(categoryId.Value), ct);
            var categorySet = categoryIds.ToHashSet();
            filtered = filtered.Where(x => x.CategoryId.HasValue && categorySet.Contains(x.CategoryId.Value));
        }

        if (!string.IsNullOrWhiteSpace(salesModel))
        {
            var requested = Enum.Parse<SalesModel>(salesModel, ignoreCase: true);
            filtered = filtered.Where(x => x.SalesModel == (short)requested);
        }

        var map = filtered.ToDictionary(x => x.Id);
        return new SyntheticOfferQueryContext(
            true,
            resolvedShop.ShopId,
            moduleAgreementProducts.Select(x => x.Id).ToHashSet(),
            map,
            map.Values.Where(x => x.SalesModel == (short)SalesModel.StockBased).Select(x => x.Id).ToArray(),
            map.Values.Where(x => x.SalesModel == (short)SalesModel.SessionBased).Select(x => x.Id).ToArray());
    }

    private async Task<(bool IsValid, Guid? ShopId)> ResolveShopAsync(
        Guid? shopId,
        string? shopSlug,
        CancellationToken ct)
    {
        shopId = shopId == Guid.Empty ? null : shopId;
        var normalizedSlug = string.IsNullOrWhiteSpace(shopSlug)
            ? null
            : shopSlug.Trim().ToLowerInvariant();

        if (normalizedSlug is not null)
        {
            var bySlug = await _shopRepository.GetBySlugAsync(normalizedSlug, ct);
            if (bySlug is null || bySlug.Status != ShopStatus.Active)
                return (false, null);

            if (shopId.HasValue && shopId.Value != bySlug.Id)
                throw new ArgumentException("شناسه و آدرس فروشگاه با یکدیگر هم‌خوانی ندارند.");

            return (true, bySlug.Id);
        }

        if (!shopId.HasValue)
            return (true, null);

        var byId = await _shopRepository.GetByIdAsync(shopId.Value, ct);
        return byId is { Status: ShopStatus.Active }
            ? (true, byId.Id)
            : (false, null);
    }
}

public sealed record SyntheticOfferQueryContext(
    bool IsShopValid,
    Guid? ShopId,
    IReadOnlySet<Guid> ModuleAgreementProductIds,
    IReadOnlyDictionary<Guid, AgreementProductDto> AgreementProducts,
    IReadOnlyList<Guid> StockBasedAgreementProductIds,
    IReadOnlyList<Guid> SessionBasedAgreementProductIds)
{
    public static SyntheticOfferQueryContext InvalidShop { get; } =
        new(false, null, new HashSet<Guid>(), new Dictionary<Guid, AgreementProductDto>(), [], []);

    public static SyntheticOfferQueryContext Empty(Guid? shopId) =>
        new(true, shopId, new HashSet<Guid>(), new Dictionary<Guid, AgreementProductDto>(), [], []);
}
