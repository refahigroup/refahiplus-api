using MediatR;
using Refahi.Modules.Store.Application.Contracts.Queries.Cart;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Cart.GetOfferCart;

public sealed class GetOfferCartQueryHandler(ICartRepository carts, IOfferRepository offers,
    IProductRepository products, IShopRepository shops, IOnlineOfferEligibilityService eligibility,
    TimeProvider clock)
    : IRequestHandler<GetOfferCartQuery, OfferCartDto?>
{
    public async Task<OfferCartDto?> Handle(GetOfferCartQuery request, CancellationToken ct)
    {
        var cart = await carts.GetByUserAndModuleIdAsync(request.UserId, request.ModuleId, ct);
        if (cart is null) return null;
        var result = new List<OfferCartItemDto>();
        foreach (var item in cart.Items.Where(x => x.OfferId.HasValue))
        {
            var product = await products.GetByIdAsync(item.ProductId, ct);
            var shop = await shops.GetByIdAsync(item.ShopId, ct);
            var current = await offers.ResolveAsync(item.ProductId, item.ShopId, item.VariantId,
                item.SessionId, clock.GetUtcNow(), ct);
            var changed = current is null || current.Id != item.OfferId ||
                current.FinalPriceMinor != item.UnitPriceMinor ||
                current.OriginalPriceMinor != item.OriginalUnitPriceMinor;
            var available = current is not null && product is not null && shop is not null;
            var availabilityCode = available ? "AVAILABLE" : current is null ? "OFFER_NOT_EFFECTIVE" :
                product is null ? "PRODUCT_NOT_FOUND" : "SHOP_NOT_FOUND";
            var availabilityReason = available ? "قابل خرید" : current is null ?
                "پیشنهاد منقضی یا در دسترس نیست" : product is null ?
                "محصول یافت نشد" : "فروشگاه یافت نشد";
            if (available)
            {
                try
                {
                    _ = await eligibility.ResolveByIdAsync(current!.Id, item.Quantity,
                        item.VariantId, item.SessionId, item.UsageDate, ct);
                }
                catch (StoreDomainException ex)
                {
                    available = false;
                    availabilityCode = ex.ErrorCode;
                    availabilityReason = ex.Message;
                }
            }
            var variant = product?.Variants.FirstOrDefault(x => x.Id == item.VariantId);
            var variantLabel = variant is null ? null : !string.IsNullOrWhiteSpace(variant.SKU)
                ? variant.SKU
                : string.Join(" / ", variant.Combinations.Select(combination =>
                {
                    var attribute = product!.VariantAttributes.FirstOrDefault(x =>
                        x.Id == combination.VariantAttributeId);
                    return attribute?.Values.FirstOrDefault(x =>
                        x.Id == combination.VariantAttributeValueId)?.Value;
                }).Where(x => !string.IsNullOrWhiteSpace(x)));
            var sessionLabel = product?.Sessions.FirstOrDefault(x => x.Id == item.SessionId)?.Title;
            var image = variant?.ImageUrl ?? product?.Images.OrderByDescending(x => x.IsMain)
                .ThenBy(x => x.SortOrder).FirstOrDefault()?.ImageUrl;
            result.Add(new OfferCartItemDto(item.Id, item.OfferId!.Value, item.ProductId, item.ShopId,
                item.VariantId, item.SessionId, item.UsageDate, item.Quantity,
                product?.Title ?? "محصول ناموجود", product?.Slug ?? string.Empty, image,
                shop?.Name ?? "فروشگاه ناموجود", shop?.Slug ?? string.Empty, variantLabel,
                sessionLabel, product?.FulfillmentMethod.ToString() ?? string.Empty,
                item.OriginalUnitPriceMinor, item.UnitPriceMinor, current?.Id,
                current?.OriginalPriceMinor, current?.FinalPriceMinor, changed, available,
                availabilityCode, availabilityReason));
        }
        return new OfferCartDto(cart.Id, result,
            result.Sum(x => checked(x.SnapshotFinalUnitPriceMinor * x.Quantity)),
            result.Sum(x => checked((x.CurrentFinalUnitPriceMinor ?? x.SnapshotFinalUnitPriceMinor) * x.Quantity)),
            result.Sum(x => x.Quantity), result.Any(x => x.HasOfferChanged));
    }
}
