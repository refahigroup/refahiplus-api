using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Models;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Commands.Checkout;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using System.Text.Json;

namespace Refahi.Modules.Store.Application.Features.Checkout.PlaceStoreOrder;

public class PlaceStoreOrderCommandHandler : IRequestHandler<PlaceStoreOrderCommand, PlaceStoreOrderResponse>
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopRepository _shopRepo;
    private readonly IProductSessionRepository _sessionRepo;
    private readonly IStoreProductPriceResolver _priceResolver;
    private readonly IStoreInPersonFinancialPlanner _financialPlanner;
    private readonly IDeliveryService _deliveryService;
    private readonly IMediator _mediator;

    public PlaceStoreOrderCommandHandler(
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IShopRepository shopRepo,
        IProductSessionRepository sessionRepo,
        IStoreProductPriceResolver priceResolver,
        IStoreInPersonFinancialPlanner financialPlanner,
        IDeliveryService deliveryService,
        IMediator mediator)
    {
        _cartRepo = cartRepo;
        _productRepo = productRepo;
        _shopRepo = shopRepo;
        _sessionRepo = sessionRepo;
        _priceResolver = priceResolver;
        _financialPlanner = financialPlanner;
        _deliveryService = deliveryService;
        _mediator = mediator;
    }

    public async Task<PlaceStoreOrderResponse> Handle(PlaceStoreOrderCommand request, CancellationToken cancellationToken)
    {
        var orderIdempotencyKey = BuildStoreOrderIdempotencyKey(request.IdempotencyKey);
        var existingOrder = await _mediator.Send(
            new GetOrderByIdempotencyKeyQuery(orderIdempotencyKey, request.UserId, "Store"),
            cancellationToken);

        if (existingOrder is not null)
            return MapExistingOrder(existingOrder);

        // STEP 1: Load cart
        var cart = await _cartRepo.GetByUserAndModuleIdAsync(request.UserId, request.ModuleId, cancellationToken);

        if (cart is null || !cart.Items.Any())
            throw new StoreDomainException("سبد خرید خالی است", "CART_EMPTY");

        var manualResult = await TryPlaceManualOrderAsync(cart, request, orderIdempotencyKey, cancellationToken);
        if (manualResult is not null) return manualResult;

        // STEP 2: Validate all products and build order items
        Guid? shopId = null;
        var orderItems = new List<CreateOrderItemInput>();
        var stockUpdates = new List<(Guid ProductId, Guid? VariantId, int Quantity)>();
        var sessionUpdates = new List<(Guid ProductId, Guid SessionId, int Quantity)>();
        var deliveryItems = new List<DeliveryItemInput>();

        // Cache agreement products per unique AgreementProductId
        var agreementProductCache = new Dictionary<Guid, Refahi.Modules.SupplyChain.Application.Contracts.Dtos.AgreementProductDto?>();
        var shopCache = new Dictionary<Guid, Refahi.Modules.Store.Domain.Aggregates.Shop?>();

        foreach (var cartItem in cart.Items)
        {
            // Single-shop rule (via CartItem.ShopId)
            if (shopId.HasValue && cartItem.ShopId != shopId.Value)
                throw new StoreDomainException("تمامی محصولات باید از یک فروشگاه باشند", "MIXED_SHOP_ITEMS");
            shopId = cartItem.ShopId;

            var product = await _productRepo.GetByIdAsync(cartItem.ProductId, cancellationToken);
            if (product is null || product.IsDeleted)
                throw new StoreDomainException($"محصول '{cartItem.ProductId}' یافت نشد یا حذف شده است", "PRODUCT_NOT_FOUND");

            if (!product.IsAvailable)
                throw new StoreDomainException($"محصول '{product.Title}' در حال حاضر قابل خرید نیست", "PRODUCT_NOT_AVAILABLE");

            if (!shopCache.TryGetValue(cartItem.ShopId, out var shopForTitle))
            {
                shopForTitle = await _shopRepo.GetByIdAsync(cartItem.ShopId, cancellationToken);
                shopCache[cartItem.ShopId] = shopForTitle;
            }

            if (shopForTitle is null)
                throw new StoreDomainException("فروشگاه انتخاب‌شده یافت نشد", "SHOP_NOT_FOUND");

            if (shopForTitle.Status != ShopStatus.Active)
                throw new StoreDomainException("فروشگاه انتخاب‌شده فعال نیست", "SHOP_NOT_ACTIVE");

            // Get AgreementProduct (cached)
            if (!agreementProductCache.TryGetValue(product.AgreementProductId, out var ap))
            {
                ap = await _mediator.Send(new GetAgreementProductByIdQuery(product.AgreementProductId), cancellationToken);
                agreementProductCache[product.AgreementProductId] = ap;
            }

            if (ap is null)
                throw new StoreDomainException("اطلاعات محصول یافت نشد", "AGREEMENT_PRODUCT_NOT_FOUND");

            var salesModel = (SalesModel)ap.SalesModel;

            // CategoryCode via References
            string? categoryCode = null;
            if (ap?.CategoryId.HasValue == true)
            {
                var category = await _mediator.Send(new GetCategoryByIdQuery(ap.CategoryId.Value), cancellationToken);
                categoryCode = category?.CategoryCode;
            }

            var priceVariantId = salesModel == SalesModel.SessionBased && cartItem.SessionId.HasValue
                ? null
                : cartItem.VariantId;
            var resolvedPrice = await _priceResolver.ResolveAsync(
                cartItem.ShopId,
                product,
                priceVariantId,
                cancellationToken);
            long authoritativeUnitPrice = resolvedPrice.UnitPriceMinor;

            // Build metadata from AgreementProduct
            string itemTitle;
            string metadataJson;

            if (salesModel == SalesModel.StockBased)
            {
                if (cartItem.SessionId.HasValue)
                    throw new StoreDomainException("سانس برای محصول موجودی‌محور معتبر نیست", "INVALID_SESSION_FOR_STOCK_PRODUCT");

                if (cartItem.VariantId.HasValue)
                {
                    var variant = product.Variants.FirstOrDefault(v => v.Id == cartItem.VariantId.Value)
                        ?? throw new StoreDomainException($"تنوع محصول '{product.Title}' یافت نشد", "VARIANT_NOT_FOUND");

                    if (!variant.HasLegacyStockAvailable(cartItem.Quantity))
                        throw new StoreDomainException($"موجودی کافی برای '{product.Title}' وجود ندارد", "INSUFFICIENT_STOCK");

                    var variantLabel = !string.IsNullOrWhiteSpace(variant.SKU)
                        ? variant.SKU
                        : string.Join("/", variant.Combinations.Select(c =>
                        {
                            var attr = product.VariantAttributes.FirstOrDefault(a => a.Id == c.VariantAttributeId);
                            var val = attr?.Values.FirstOrDefault(v => v.Id == c.VariantAttributeValueId);
                            return val?.Value ?? string.Empty;
                        }).Where(s => !string.IsNullOrEmpty(s)));

                    itemTitle = $"{product.Title}{(string.IsNullOrEmpty(variantLabel) ? string.Empty : $" - {variantLabel}")} - {shopForTitle?.Name ?? string.Empty}";
                }
                else
                {
                    if (product.StockCount < cartItem.Quantity)
                        throw new StoreDomainException($"موجودی کافی برای '{product.Title}' وجود ندارد", "INSUFFICIENT_STOCK");

                    itemTitle = $"{product.Title} - {shopForTitle?.Name ?? string.Empty}";
                }

                metadataJson = JsonSerializer.Serialize(new
                {
                    source_module = "Store",
                    shop_id = cartItem.ShopId.ToString(),
                    agreement_product_id = ap.Id.ToString(),
                    commission_percent = ap.CommissionPercent,
                    gross_amount_minor = authoritativeUnitPrice * cartItem.Quantity,
                    commission_amount_minor = CalculateCommission(authoritativeUnitPrice * cartItem.Quantity, ap.CommissionPercent),
                    net_amount_minor = authoritativeUnitPrice * cartItem.Quantity - CalculateCommission(authoritativeUnitPrice * cartItem.Quantity, ap.CommissionPercent),
                    product_id = cartItem.ProductId.ToString(),
                    product_type = ap?.ProductType.ToString(),
                    sales_model = salesModel.ToString(),
                    delivery_type = ap?.DeliveryType.ToString(),
                    variant_id = cartItem.VariantId?.ToString(),
                    shop_product_id = resolvedPrice.ShopProductId.ToString(),
                    shop_product_variant_id = resolvedPrice.ShopProductVariantId?.ToString(),
                    price_source = resolvedPrice.Source.ToString(),
                    unit_price_minor = authoritativeUnitPrice,
                    original_unit_price_minor = resolvedPrice.OriginalPriceMinor,
                    discounted_price_minor = resolvedPrice.DiscountedPriceMinor
                });
            }
            else // SessionBased
            {
                if (cartItem.SessionId.HasValue)
                {
                    var session = product.Sessions.FirstOrDefault(s => s.Id == cartItem.SessionId.Value)
                        ?? throw new StoreDomainException($"سانس محصول '{product.Title}' یافت نشد", "SESSION_NOT_FOUND");

                    if (!session.IsAvailable || session.RemainingCapacity < cartItem.Quantity)
                        throw new StoreDomainException($"ظرفیت کافی برای سانس '{product.Title}' وجود ندارد", "INSUFFICIENT_CAPACITY");

                    authoritativeUnitPrice += session.PriceAdjustment;

                    var sessionTitlePart = !string.IsNullOrWhiteSpace(session.Title) ? $" {session.Title}" : string.Empty;
                    itemTitle = $"{product.Title}{sessionTitlePart} {session.Date:yyyy-MM-dd} - {shopForTitle?.Name ?? string.Empty}";

                    metadataJson = JsonSerializer.Serialize(new
                    {
                        source_module = "Store",
                        shop_id = cartItem.ShopId.ToString(),
                        agreement_product_id = ap.Id.ToString(),
                        commission_percent = ap.CommissionPercent,
                        gross_amount_minor = authoritativeUnitPrice * cartItem.Quantity,
                        commission_amount_minor = CalculateCommission(authoritativeUnitPrice * cartItem.Quantity, ap.CommissionPercent),
                        net_amount_minor = authoritativeUnitPrice * cartItem.Quantity - CalculateCommission(authoritativeUnitPrice * cartItem.Quantity, ap.CommissionPercent),
                        product_id = cartItem.ProductId.ToString(),
                        product_type = ap?.ProductType.ToString(),
                        sales_model = salesModel.ToString(),
                        delivery_type = ap?.DeliveryType.ToString(),
                        session_id = cartItem.SessionId.Value.ToString(),
                        date = session.Date.ToString("yyyy-MM-dd"),
                        start_time = session.StartTime.ToString("HH:mm"),
                        end_time = session.EndTime.ToString("HH:mm"),
                        shop_product_id = resolvedPrice.ShopProductId.ToString(),
                        shop_product_variant_id = resolvedPrice.ShopProductVariantId?.ToString(),
                        price_source = resolvedPrice.Source.ToString(),
                        unit_price_minor = authoritativeUnitPrice,
                        original_unit_price_minor = resolvedPrice.OriginalPriceMinor + session.PriceAdjustment,
                        discounted_price_minor = resolvedPrice.DiscountedPriceMinor
                    });
                }
                else if (cartItem.VariantId.HasValue)
                {
                    var variant = product.Variants.FirstOrDefault(v => v.Id == cartItem.VariantId.Value)
                        ?? throw new StoreDomainException($"تنوع محصول '{product.Title}' یافت نشد", "VARIANT_NOT_FOUND");

                    var normalizedUsageDate = StoreVariantCapacityService.NormalizeAndValidateUsageDate(variant, cartItem.UsageDate);
                    await StoreVariantCapacityService.EnsureCapacityAvailableAsync(
                        variant,
                        normalizedUsageDate,
                        cartItem.Quantity,
                        _mediator,
                        excludeOrderId: null,
                        cancellationToken);

                    var variantLabel = !string.IsNullOrWhiteSpace(variant.SKU)
                        ? variant.SKU
                        : string.Join("/", variant.Combinations.Select(c =>
                        {
                            var attr = product.VariantAttributes.FirstOrDefault(a => a.Id == c.VariantAttributeId);
                            var val = attr?.Values.FirstOrDefault(v => v.Id == c.VariantAttributeValueId);
                            return val?.Value ?? string.Empty;
                        }).Where(s => !string.IsNullOrEmpty(s)));

                    itemTitle = $"{product.Title}{(string.IsNullOrEmpty(variantLabel) ? string.Empty : $" - {variantLabel}")} - {shopForTitle?.Name ?? string.Empty}";

                    metadataJson = JsonSerializer.Serialize(new
                    {
                        source_module = "Store",
                        shop_id = cartItem.ShopId.ToString(),
                        agreement_product_id = ap.Id.ToString(),
                        commission_percent = ap.CommissionPercent,
                        gross_amount_minor = authoritativeUnitPrice * cartItem.Quantity,
                        commission_amount_minor = CalculateCommission(authoritativeUnitPrice * cartItem.Quantity, ap.CommissionPercent),
                        net_amount_minor = authoritativeUnitPrice * cartItem.Quantity - CalculateCommission(authoritativeUnitPrice * cartItem.Quantity, ap.CommissionPercent),
                        product_id = cartItem.ProductId.ToString(),
                        product_type = ap?.ProductType.ToString(),
                        sales_model = salesModel.ToString(),
                        delivery_type = ap?.DeliveryType.ToString(),
                        variant_id = cartItem.VariantId.Value.ToString(),
                        usage_date = normalizedUsageDate?.ToString("yyyy-MM-dd"),
                        capacity_type = variant.CapacityType.ToString(),
                        from_date = variant.FromDate?.ToString("yyyy-MM-dd"),
                        to_date = variant.ToDate?.ToString("yyyy-MM-dd"),
                        shop_product_id = resolvedPrice.ShopProductId.ToString(),
                        shop_product_variant_id = resolvedPrice.ShopProductVariantId?.ToString(),
                        price_source = resolvedPrice.Source.ToString(),
                        unit_price_minor = authoritativeUnitPrice,
                        original_unit_price_minor = resolvedPrice.OriginalPriceMinor,
                        discounted_price_minor = resolvedPrice.DiscountedPriceMinor
                    });
                }
                else
                {
                    throw new StoreDomainException("برای محصولات سانسی، انتخاب سانس یا خدمت الزامی است", "SESSION_REQUIRED");
                }
            }

            if (authoritativeUnitPrice != cartItem.UnitPriceMinor)
                throw new StoreDomainException(
                    "قیمت برخی آیتم‌های سبد خرید تغییر کرده است. لطفاً سبد خرید را به‌روزرسانی و دوباره تلاش کنید.",
                    "CART_PRICE_CHANGED");

            // روش ارسال این آیتم
            short deliveryMethod = 0;
            if (request.CartItemDeliveryMethods is not null
                && request.CartItemDeliveryMethods.TryGetValue(cartItem.Id, out var dm))
            {
                deliveryMethod = dm;
            }

            deliveryItems.Add(new DeliveryItemInput(deliveryMethod, cartItem.Quantity));

            orderItems.Add(new CreateOrderItemInput(
                Title: itemTitle,
                UnitPriceMinor: authoritativeUnitPrice,
                Quantity: cartItem.Quantity,
                DiscountAmountMinor: 0,
                SourceItemId: cartItem.ProductId,
                CategoryCode: categoryCode ?? string.Empty,
                Tags: null,
                MetadataJson: metadataJson,
                DeliveryMethod: deliveryMethod));
        }

        // STEP 3: دریافت آدرس و ساخت Snapshot
        UserAddressDto? addressDto = null;
        string? addressSnapshotJson = null;
        if (request.ShippingAddressId.HasValue)
        {
            addressDto = await _mediator.Send(
                new GetUserAddressByIdQuery(request.ShippingAddressId.Value, request.UserId),
                cancellationToken);

            if (addressDto is null)
                throw new StoreDomainException("آدرس ارسال نامعتبر است یا متعلق به این کاربر نیست", "INVALID_SHIPPING_ADDRESS");

            addressSnapshotJson = JsonSerializer.Serialize(new
            {
                id = addressDto.Id,
                title = addressDto.Title,
                province_id = addressDto.ProvinceId,
                city_id = addressDto.CityId,
                full_address = addressDto.FullAddress,
                postal_code = addressDto.PostalCode,
                receiver_name = addressDto.ReceiverName,
                receiver_phone = addressDto.ReceiverPhone,
                plate = addressDto.Plate,
                unit = addressDto.Unit,
                latitude = addressDto.Latitude,
                longitude = addressDto.Longitude
            });
        }

        // STEP 4: محاسبه‌ی هزینه ارسال
        var shippingFeeMinor = _deliveryService.CalcPrice(
            deliveryItems,
            shippingAddressId: request.ShippingAddressId,
            shopId: shopId);

        // STEP 5: کد تخفیف (فاز ۱: Stub — همیشه ۰)
        long discountCodeAmountMinor = 0;
        // TODO: در فاز ۳ پیاده‌سازی واقعی validation و محاسبه‌ی کد تخفیف.

        // STEP 6: Create order via Orders module
        var createOrderCommand = new CreateOrderCommand(
            UserId: request.UserId,
            SourceModule: "Store",
            SourceReferenceId: shopId!.Value,
            Items: orderItems,
            IdempotencyKey: orderIdempotencyKey,
            ShippingAddressId: request.ShippingAddressId,
            ShippingAddressSnapshotJson: addressSnapshotJson,
            DeliveryDate: request.DeliveryDate,
            DeliveryTimeSlot: request.DeliveryTimeSlot,
            ShippingFeeMinor: shippingFeeMinor,
            DiscountCode: request.DiscountCode,
            DiscountCodeAmountMinor: discountCodeAmountMinor,
            SourceOwnerId: shopCache[shopId.Value]!.SupplierId,
            SourceShopId: shopId.Value);

        var orderResult = await _mediator.Send(createOrderCommand, cancellationToken);

        // STEP 7: Payment is handled by Orders unified checkout.
        var paymentStatus = "Unpaid";

        // STEP 8: Decrease stock/capacity ONLY after payment success
        foreach (var (productId, variantId, quantity) in stockUpdates)
        {
            var product = await _productRepo.GetByIdAsync(productId, cancellationToken);
            if (product is null) continue;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    if (variantId.HasValue)
                        product.DecreaseVariantStock(variantId.Value, quantity);
                    else
                        product.DecreaseStock(quantity);

                    await _productRepo.UpdateAsync(product, cancellationToken);
                    break;
                }
                catch (StoreConcurrencyException) when (attempt < 3)
                {
                    product = await _productRepo.GetByIdAsync(productId, cancellationToken)
                        ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");
                    await Task.Delay(50 * attempt, cancellationToken);
                }
                catch (StoreConcurrencyException)
                {
                    throw new StoreDomainException(
                        "به دلیل تقاضای همزمان زیاد، خرید موفق نشد. لطفاً مجدداً تلاش کنید",
                        "CONCURRENCY_CONFLICT");
                }
            }
        }

        foreach (var (productId, sessionId, quantity) in sessionUpdates)
        {
            var session = await _sessionRepo.GetByIdAsync(sessionId, cancellationToken);
            if (session is not null)
            {
                session.Sell(quantity);
                await _sessionRepo.UpdateAsync(session, cancellationToken);
            }
        }

        // Cart is cleared after the order is paid by the unified checkout flow.

        // STEP 10: Return response
        return new PlaceStoreOrderResponse(
            OrderId: orderResult.OrderId,
            OrderNumber: orderResult.OrderNumber,
            FinalAmountMinor: orderResult.FinalAmountMinor,
            Status: paymentStatus);
    }

    private static long CalculateCommission(long grossAmountMinor, decimal commissionPercent)
        => checked((long)Math.Round(
            grossAmountMinor * commissionPercent / 100m,
            0,
            MidpointRounding.AwayFromZero));

    private async Task<PlaceStoreOrderResponse?> TryPlaceManualOrderAsync(
        Refahi.Modules.Store.Domain.Aggregates.Cart cart,
        PlaceStoreOrderCommand request,
        string orderIdempotencyKey,
        CancellationToken ct)
    {
        var resolved = new List<(Refahi.Modules.Store.Domain.Entities.CartItem Item,
            Refahi.Modules.Store.Domain.Aggregates.Product Product,
            Refahi.Modules.SupplyChain.Application.Contracts.Dtos.AgreementProductDto AgreementProduct)>();
        foreach (var item in cart.Items)
        {
            var product = await _productRepo.GetByIdAsync(item.ProductId, ct)
                ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");
            var ap = await _mediator.Send(new GetAgreementProductByIdQuery(product.AgreementProductId), ct)
                ?? throw new StoreDomainException("محصول قرارداد یافت نشد", "AGREEMENT_PRODUCT_NOT_FOUND");
            resolved.Add((item, product, ap));
        }

        var manualItems = resolved.Where(x => x.AgreementProduct.PricingMode == (short)PricingMode.Manual).ToList();
        if (manualItems.Count == 0) return null;
        if (resolved.Count != 1 || manualItems.Count != 1)
            throw new StoreDomainException("سفارش حضوری فقط می‌تواند شامل یک آیتم باشد", "MANUAL_CART_MUST_BE_SINGLE_ITEM");

        var selected = manualItems[0];
        if (selected.Item.Quantity != 1 || selected.Item.VariantId.HasValue || selected.Item.SessionId.HasValue ||
            selected.Item.UsageDate.HasValue || selected.Item.UnitPriceMinor <= 0 ||
            selected.AgreementProduct.DeliveryType != (short)DeliveryType.InPerson ||
            selected.AgreementProduct.SalesModel != (short)SalesModel.Unlimited ||
            !selected.Product.IsAvailable || selected.Product.IsDeleted)
            throw new StoreDomainException("ساختار محصول حضوری معتبر نیست", "INVALID_MANUAL_PRODUCT");

        var shop = await _shopRepo.GetByIdAsync(selected.Item.ShopId, ct)
            ?? throw new StoreDomainException("فروشگاه یافت نشد", "SHOP_NOT_FOUND");
        if (shop.Status != ShopStatus.Active || shop.ShopType != ShopType.Physical ||
            selected.AgreementProduct.SupplierId != shop.SupplierId)
            throw new StoreDomainException("فروشگاه حضوری معتبر یا فعال نیست", "INVALID_IN_PERSON_SHOP");

        var financial = await _financialPlanner.BuildAsync(shop.SupplierId, selected.Item.UnitPriceMinor,
            selected.AgreementProduct.CommissionPercent, selected.AgreementProduct.VatApplicable, ct);
        string categoryCode = "store.in-person";
        if (selected.AgreementProduct.CategoryId.HasValue)
        {
            var category = await _mediator.Send(new GetCategoryByIdQuery(selected.AgreementProduct.CategoryId.Value), ct);
            categoryCode = category?.CategoryCode ?? categoryCode;
        }

        var metadata = JsonSerializer.Serialize(new
        {
            source_module = "Store", shop_id = shop.Id, product_id = selected.Product.Id,
            agreement_product_id = selected.AgreementProduct.Id,
            pricing_mode = "Manual", gross_amount_minor = financial.GrossAmountMinor,
            commission_percent = financial.CommissionPercent,
            commission_amount_minor = financial.CommissionAmountMinor,
            vat_percent = financial.VatPercent, vat_amount_minor = financial.VatAmountMinor,
            vendor_net_amount_minor = financial.VendorNetAmountMinor
        });

        var created = await _mediator.Send(new CreateOrderCommand(
            UserId: request.UserId, SourceModule: "Store", SourceReferenceId: null,
            Items: [new CreateOrderItemInput(selected.Product.Title, selected.Item.UnitPriceMinor, 1, 0,
                selected.Product.Id, categoryCode, null, metadata)],
            IdempotencyKey: orderIdempotencyKey, ReferenceType: "StoreInPerson",
            SourceOwnerId: shop.SupplierId, SourceShopId: shop.Id, CreatedByUserId: request.UserId,
            FinancialSnapshot: new OrderFinancialSnapshotInput(financial.GrossAmountMinor,
                financial.CommissionPercent, financial.CommissionAmountMinor, financial.VatPercent,
                financial.VatAmountMinor, financial.VendorNetAmountMinor),
            PaymentPostings: financial.Postings), ct);

        return new PlaceStoreOrderResponse(created.OrderId, created.OrderNumber,
            created.FinalAmountMinor, "Unpaid");
    }

    private static string BuildStoreOrderIdempotencyKey(string idempotencyKey)
        => $"store-order-{idempotencyKey}";

    private static PlaceStoreOrderResponse MapExistingOrder(OrderDto order)
        => new(
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            FinalAmountMinor: order.FinalAmountMinor,
            Status: order.PaymentState);
}
