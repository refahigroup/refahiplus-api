using System.Text.Json;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Commands.Checkout;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Checkout.PlaceStoreOrderV3;

public sealed class PlaceStoreOrderV3CommandHandler(
    ICartRepository carts, IStoreOrderRepository storeOrders, IOfferRepository offers,
    IOnlineOfferEligibilityService eligibility, IMediator mediator, TimeProvider clock)
    : IRequestHandler<PlaceStoreOrderV3Command, PlaceStoreOrderV3Response>
{
    public async Task<PlaceStoreOrderV3Response> Handle(PlaceStoreOrderV3Command request, CancellationToken ct)
    {
        var requestFingerprint = CheckoutRequestFingerprint.Create(request);
        var existing = await storeOrders.GetByIdempotencyKeyAsync(request.UserId, request.IdempotencyKey, ct);
        if (existing is not null)
        {
            existing.EnsureRequestFingerprint(requestFingerprint);
            return await ResumeAsync(existing, ct);
        }

        var cart = await carts.GetByUserAndModuleIdAsync(request.UserId, request.ModuleId, ct);
        if (cart is null || cart.Items.Count == 0)
            throw new StoreDomainException("سبد خرید خالی است", "CART_EMPTY");
        if (cart.Items.Any(x => !x.OfferId.HasValue))
            throw new StoreDomainException("سبد خرید شامل آیتم قدیمی است؛ سبد v3 را دوباره ایجاد کنید", "LEGACY_CART_ITEM_NOT_SUPPORTED");
        if (cart.Items.Select(x => x.ShopId).Distinct().Count() != 1)
            throw new StoreDomainException("تمامی محصولات باید از یک فروشگاه باشند", "MIXED_SHOP_ITEMS");

        var snapshots = new List<StoreOrderItemSnapshot>();
        var drift = new List<OfferChangedDetail>();
        foreach (var item in cart.Items)
        {
            var current = await offers.ResolveAsync(item.ProductId, item.ShopId, item.VariantId,
                item.SessionId, clock.GetUtcNow(), ct);
            if (current is null || current.Id != item.OfferId || current.FinalPriceMinor != item.UnitPriceMinor ||
                current.OriginalPriceMinor != item.OriginalUnitPriceMinor)
            {
                drift.Add(new OfferChangedDetail(item.Id, item.OfferId!.Value, item.UnitPriceMinor,
                    current?.Id, current?.FinalPriceMinor, current is null ? "پیشنهاد دیگر معتبر نیست" : "پیشنهاد یا قیمت جاری تغییر کرده است"));
                continue;
            }

            OnlineOfferContext context;
            try
            {
                context = await eligibility.ResolveByIdAsync(item.OfferId!.Value, item.Quantity,
                    item.VariantId, item.SessionId, item.UsageDate, ct);
            }
            catch (StoreDomainException ex) when (ex.ErrorCode.StartsWith("OFFER_", StringComparison.Ordinal) ||
                                                  ex.ErrorCode == "AGREEMENT_TERM_NOT_EFFECTIVE")
            {
                drift.Add(new OfferChangedDetail(item.Id, item.OfferId.Value, item.UnitPriceMinor,
                    current.Id, current.FinalPriceMinor, ex.Message));
                continue;
            }

            if (context.Product.SalesModel == SalesModel.SessionBased && item.VariantId.HasValue)
            {
                var variant = context.Product.Variants.Single(x => x.Id == item.VariantId.Value);
                var usage = StoreVariantCapacityService.NormalizeAndValidateUsageDate(variant, item.UsageDate);
                await StoreVariantCapacityService.EnsureCapacityAvailableAsync(variant, usage, item.Quantity,
                    mediator, excludeOrderId: null, ct);
            }

            var category = await mediator.Send(new GetCategoryByIdQuery(context.Product.CategoryId), ct)
                ?? throw new StoreDomainException("دسته‌بندی محصول یافت نشد", "CATEGORY_NOT_FOUND");
            if (string.IsNullOrWhiteSpace(category.CategoryCode))
                throw new StoreDomainException("کد دسته‌بندی محصول معتبر نیست", "CATEGORY_CODE_REQUIRED");

            snapshots.Add(new StoreOrderItemSnapshot(
                item.Id, context.Product.Id, item.VariantId, item.SessionId, context.Offer.Id,
                context.Product.Title, GetVariantTitle(context.Product, item.VariantId),
                GetSessionTitle(context.Product, item.SessionId), context.Product.CategoryId,
                category.CategoryCode, context.Product.SupplierId, context.Shop.Id, SalesChannel.Online,
                context.Product.ProductType, context.Product.SalesModel, context.Product.FulfillmentMethod,
                item.Quantity, context.Offer.OriginalPriceMinor, context.Offer.DiscountPercent,
                context.Offer.FinalPriceMinor, context.Term.AgreementId, context.Term.TermId,
                context.Term.CommissionPercent, item.UsageDate,
                ResolveDeliveryMethod(context.Product.FulfillmentMethod, item.Id,
                    request.CartItemDeliveryMethods)));
        }
        if (drift.Count > 0) throw new OfferChangedException(drift);

        var requiresShipping = snapshots.Any(x => x.FulfillmentMethod == FulfillmentMethod.Shipping);
        if (requiresShipping && (!request.ShippingAddressId.HasValue || !request.DeliveryDate.HasValue))
            throw new StoreDomainException("آدرس و تاریخ ارسال الزامی است", "SHIPPING_DETAILS_REQUIRED");
        if (requiresShipping)
        {
            var address = await mediator.Send(new GetUserAddressByIdQuery(
                request.ShippingAddressId!.Value, request.UserId), ct);
            if (address is null)
                throw new StoreDomainException("آدرس ارسال نامعتبر است یا متعلق به این کاربر نیست", "INVALID_SHIPPING_ADDRESS");
        }

        var shopId = snapshots[0].ShopId;
        var supplierId = snapshots[0].SupplierId;
        var storeOrder = StoreOrder.Create(request.UserId, request.ModuleId, shopId, supplierId,
            request.IdempotencyKey, requestFingerprint, snapshots,
            requiresShipping ? request.ShippingAddressId : null,
            requiresShipping ? request.DeliveryDate : null,
            requiresShipping ? request.DeliveryTimeSlot : (short)0);
        try { await storeOrders.AddAsync(storeOrder, ct); }
        catch (StoreDomainException ex) when (ex.ErrorCode == "IDEMPOTENCY_CONFLICT")
        {
            var concurrent = await storeOrders.GetByIdempotencyKeyAsync(request.UserId, request.IdempotencyKey, ct);
            if (concurrent is null) throw;
            concurrent.EnsureRequestFingerprint(requestFingerprint);
            storeOrder = concurrent;
        }
        return await ResumeAsync(storeOrder, ct);
    }

    private async Task<PlaceStoreOrderV3Response> ResumeAsync(StoreOrder storeOrder, CancellationToken ct)
    {
        var key = BuildOrderKey(storeOrder.Id);
        if (storeOrder.OrderId.HasValue)
        {
            var existingOrder = await mediator.Send(new GetOrderByIdempotencyKeyQuery(key, storeOrder.UserId, "Store"), ct)
                ?? throw new StoreDomainException("سفارش متصل‌شده یافت نشد", "ATTACHED_ORDER_NOT_FOUND");
            return Map(storeOrder, existingOrder.Id, existingOrder.OrderNumber, existingOrder.FinalAmountMinor);
        }

        var items = storeOrder.Items.Select(x => new CreateOrderItemInput(
            BuildTitle(x), x.FinalUnitPriceMinor, x.Quantity, 0, x.Id, x.CategoryCode,
            ["store", "online", $"shop:{x.ShopId:N}"], JsonSerializer.Serialize(new
            {
                store_order_id = storeOrder.Id, store_order_item_id = x.Id, offer_id = x.OfferId,
                product_id = x.ProductId, variant_id = x.ProductVariantId, session_id = x.ProductSessionId,
                agreement_id = x.AgreementId, agreement_category_term_id = x.AgreementCategoryTermId,
                commission_percent = x.CommissionPercent, commission_amount_minor = x.CommissionAmountMinor,
                sales_model = x.SalesModel.ToString(), fulfillment_method = x.FulfillmentMethod.ToString(),
                usage_date = x.UsageDate
            }), x.DeliveryMethod)).ToList();

        var requiresShipping = storeOrder.Items.Any(x => x.FulfillmentMethod == FulfillmentMethod.Shipping);

        var created = await mediator.Send(new CreateOrderCommand(
            storeOrder.UserId, "Store", storeOrder.Id, items, key, ReferenceType: "StoreOrder",
            ShippingAddressId: requiresShipping ? storeOrder.ShippingAddressId : null,
            DeliveryDate: requiresShipping ? storeOrder.DeliveryDate : null,
            DeliveryTimeSlot: requiresShipping ? storeOrder.DeliveryTimeSlot : (short)0,
            SourceOwnerId: storeOrder.SupplierId, SourceShopId: storeOrder.ShopId), ct);
        storeOrder.AttachOrder(created.OrderId);
        await storeOrders.UpdateAsync(storeOrder, ct);
        return Map(storeOrder, created.OrderId, created.OrderNumber, created.FinalAmountMinor);
    }

    private static short ResolveDeliveryMethod(FulfillmentMethod fulfillmentMethod, Guid cartItemId,
        Dictionary<Guid, short>? selections)
    {
        if (fulfillmentMethod == FulfillmentMethod.Pickup) return 3;
        if (fulfillmentMethod != FulfillmentMethod.Shipping) return 0;
        if (selections is null || !selections.TryGetValue(cartItemId, out var method) || method is not (1 or 2))
            throw new StoreDomainException("روش ارسال آیتم الزامی است", "DELIVERY_METHOD_REQUIRED");
        return method;
    }

    private static string BuildTitle(StoreOrderItem x) => string.Join(" - ",
        new[] { x.ProductTitle, x.VariantTitle, x.SessionTitle }.Where(v => !string.IsNullOrWhiteSpace(v)));
    private static string? GetVariantTitle(Product product, Guid? id) => id.HasValue
        ? product.Variants.FirstOrDefault(x => x.Id == id)?.SKU : null;
    private static string? GetSessionTitle(Product product, Guid? id) => id.HasValue
        ? product.Sessions.FirstOrDefault(x => x.Id == id)?.Title : null;
    private static string BuildOrderKey(Guid id) => $"store-order-v3:{id:N}";
    private static PlaceStoreOrderV3Response Map(StoreOrder storeOrder, Guid orderId,
        string orderNumber, long amount) => new(storeOrder.Id, orderId, orderNumber, amount,
        storeOrder.Status.ToString(), $"/checkout/orders/{orderId}");
}
