using MediatR;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementCategoryTerms;

namespace Refahi.Modules.Store.Application.Services;

public interface IOnlineOfferEligibilityService
{
    Task<OnlineOfferContext> ResolveByIdAsync(Guid offerId, int quantity,
        Guid? variantId, Guid? sessionId, DateOnly? usageDate, CancellationToken ct);
}

public sealed class OnlineOfferEligibilityService(
    IOfferRepository offers, IProductRepository products, IShopRepository shops,
    IMediator mediator, TimeProvider clock) : IOnlineOfferEligibilityService
{
    public async Task<OnlineOfferContext> ResolveByIdAsync(Guid offerId, int quantity,
        Guid? variantId, Guid? sessionId, DateOnly? usageDate, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var offer = await offers.GetByIdAsync(offerId, includeDeleted: true, ct)
            ?? throw new StoreDomainException("پیشنهاد یافت نشد", "OFFER_NOT_FOUND");
        if (!offer.IsEffectiveAt(now))
            throw new StoreDomainException("پیشنهاد در حال حاضر معتبر نیست", "OFFER_NOT_EFFECTIVE");
        if (offer.ProductVariantId != variantId || offer.ProductSessionId != sessionId)
            throw new StoreDomainException("انتخاب تنوع یا سانس با پیشنهاد مطابقت ندارد", "OFFER_SELECTION_MISMATCH");

        var product = await products.GetByIdAsync(offer.ProductId, ct)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");
        if (product.IsDeleted || !product.IsAvailable || product.SupplierId == Guid.Empty)
            throw new StoreDomainException("محصول در حال حاضر قابل خرید نیست", "PRODUCT_NOT_AVAILABLE");
        var shop = await shops.GetByIdAsync(offer.ShopId, ct)
            ?? throw new StoreDomainException("فروشگاه یافت نشد", "SHOP_NOT_FOUND");
        if (shop.Status != ShopStatus.Active || shop.ShopType != ShopType.Online)
            throw new StoreDomainException("فروشگاه آنلاین فعال نیست", "SHOP_NOT_ONLINE");
        if (shop.SupplierId != product.SupplierId)
            throw new StoreDomainException("محصول متعلق به تامین‌کننده فروشگاه نیست", "SHOP_PRODUCT_OWNERSHIP_MISMATCH");

        var term = await mediator.Send(new ResolveAgreementCategoryTermQuery(
            product.SupplierId, product.CategoryId, (short)SalesChannel.Online, now), ct)
            ?? throw new StoreDomainException("قرارداد آنلاین معتبری برای این محصول وجود ندارد", "AGREEMENT_TERM_NOT_EFFECTIVE");

        ValidateAvailability(product, offer, quantity, usageDate);
        return new OnlineOfferContext(offer, product, shop, term, usageDate);
    }

    public static void ValidateAvailability(Product product, Offer offer, int quantity, DateOnly? usageDate)
    {
        if (quantity <= 0) throw new StoreDomainException("تعداد باید بیشتر از صفر باشد", "INVALID_QUANTITY");
        if (product.SalesModel == SalesModel.InventoryBased)
        {
            if (offer.ProductSessionId.HasValue)
                throw new StoreDomainException("سانس برای محصول موجودی‌محور معتبر نیست", "INVALID_SESSION_SELECTION");
            if (offer.ProductVariantId.HasValue)
            {
                var variant = product.Variants.FirstOrDefault(x => x.Id == offer.ProductVariantId)
                    ?? throw new StoreDomainException("تنوع محصول یافت نشد", "VARIANT_NOT_FOUND");
                if (!variant.IsAvailable || !variant.HasLegacyStockAvailable(quantity))
                    throw new StoreDomainException("موجودی کافی نیست", "INSUFFICIENT_STOCK");
            }
            else if (product.StockCount < quantity)
                throw new StoreDomainException("موجودی کافی نیست", "INSUFFICIENT_STOCK");
        }
        else if (product.SalesModel == SalesModel.SessionBased)
        {
            if (offer.ProductSessionId.HasValue)
            {
                var session = product.Sessions.FirstOrDefault(x => x.Id == offer.ProductSessionId)
                    ?? throw new StoreDomainException("سانس یافت نشد", "SESSION_NOT_FOUND");
                if (!session.IsAvailable || session.RemainingCapacity < quantity)
                    throw new StoreDomainException("ظرفیت کافی نیست", "INSUFFICIENT_CAPACITY");
            }
            else if (offer.ProductVariantId.HasValue)
            {
                var variant = product.Variants.FirstOrDefault(x => x.Id == offer.ProductVariantId)
                    ?? throw new StoreDomainException("تنوع محصول یافت نشد", "VARIANT_NOT_FOUND");
                _ = StoreVariantCapacityService.NormalizeAndValidateUsageDate(variant, usageDate);
            }
            else throw new StoreDomainException("انتخاب سانس یا خدمت الزامی است", "SESSION_REQUIRED");
        }
        else if (usageDate.HasValue)
            throw new StoreDomainException("تاریخ استفاده برای این محصول مجاز نیست", "USAGE_DATE_NOT_ALLOWED");
    }
}

public sealed record OnlineOfferContext(Offer Offer, Product Product, Shop Shop,
    ResolvedAgreementCategoryTermDto Term, DateOnly? UsageDate);
