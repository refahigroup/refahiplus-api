using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Application.Contracts.Queries.Cart;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using CartAggregate = Refahi.Modules.Store.Domain.Aggregates.Cart;

namespace Refahi.Modules.Store.Application.Features.Cart.SyncCart;

public class SyncCartCommandHandler : IRequestHandler<SyncCartCommand, SyncCartResponse>
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IProductSessionRepository _sessionRepo;
    private readonly IMediator _mediator;
    private readonly IMemoryCache _cache;

    public SyncCartCommandHandler(
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IShopProductRepository shopProductRepo,
        IProductSessionRepository sessionRepo,
        IMediator mediator,
        IMemoryCache cache)
    {
        _cartRepo = cartRepo;
        _productRepo = productRepo;
        _shopProductRepo = shopProductRepo;
        _sessionRepo = sessionRepo;
        _mediator = mediator;
        _cache = cache;
    }

    public async Task<SyncCartResponse> Handle(SyncCartCommand request, CancellationToken cancellationToken)
    {
        // 1. Idempotency — cache key per (UserId, IdempotencyKey)
        var cacheKey = $"sync_cart:{request.UserId}:{request.IdempotencyKey}";
        if (_cache.TryGetValue<SyncCartResponse>(cacheKey, out var cached) && cached is not null)
            return cached;

        // 2. Load or create cart
        var cart = await _cartRepo.GetByUserAndModuleIdAsync(request.UserId, request.ModuleId, cancellationToken);
        bool isNew = cart is null;
        if (isNew)
            cart = CartAggregate.Create(request.UserId, request.ModuleId);

        // 3. Determine dominant ShopId — existing server-cart shop wins
        Guid? dominantShopId = cart!.Items.FirstOrDefault()?.ShopId
            ?? request.Items.FirstOrDefault()?.ShopId;

        var warnings = new List<CartSyncWarning>();
        var mergeSpecs = new List<CartAggregate.MergeItemSpec>();

        // 4. Validate and map each incoming item
        foreach (var item in request.Items)
        {
            // 4a. Shop mismatch guard
            if (dominantShopId.HasValue && item.ShopId != dominantShopId.Value)
            {
                warnings.Add(new CartSyncWarning(
                    "SHOP_MISMATCH_DROPPED",
                    "آیتم با فروشگاه متفاوت نمی‌تواند به سبد اضافه شود",
                    item.ProductId, item.VariantId, item.SessionId));
                continue;
            }

            // 4b. Resolve product
            var product = await _productRepo.GetByIdAsync(item.ProductId, cancellationToken);
            if (product is null || product.IsDeleted)
            {
                warnings.Add(new CartSyncWarning(
                    "PRODUCT_DELETED", "محصول حذف شده است",
                    item.ProductId, item.VariantId, item.SessionId));
                continue;
            }

            // 4c. Validate shop-product link
            var shopProduct = await _shopProductRepo.GetAsync(item.ShopId, item.ProductId, cancellationToken);
            if (shopProduct is null || !shopProduct.IsActive)
            {
                warnings.Add(new CartSyncWarning(
                    "PRODUCT_DELETED", "این محصول در فروشگاه مورد نظر موجود نیست",
                    item.ProductId, item.VariantId, item.SessionId));
                continue;
            }

            // 4d. Resolve agreement product for sales model
            var ap = await _mediator.Send(new GetAgreementProductByIdQuery(product.AgreementProductId), cancellationToken);
            if (ap is null)
            {
                warnings.Add(new CartSyncWarning(
                    "PRODUCT_DELETED", "اطلاعات محصول یافت نشد",
                    item.ProductId, item.VariantId, item.SessionId));
                continue;
            }

            long authoritativePrice = shopProduct.DiscountedPrice > 0 ? shopProduct.DiscountedPrice : shopProduct.Price;
            var salesModel = (SalesModel)ap.SalesModel;
            int allowedQuantity = item.Quantity;
            DateOnly? normalizedUsageDate = null;

            if (salesModel == SalesModel.StockBased)
            {
                if (item.VariantId.HasValue)
                {
                    var variant = product.Variants.FirstOrDefault(v => v.Id == item.VariantId.Value);
                    if (variant is null || !variant.IsAvailable)
                    {
                        warnings.Add(new CartSyncWarning(
                            "VARIANT_REMOVED", "تنوع محصول موجود نیست",
                            item.ProductId, item.VariantId, item.SessionId));
                        continue;
                    }

                    authoritativePrice = variant.DiscountedPriceMinor ?? variant.PriceMinor;

                    if (variant.StockCount <= 0)
                    {
                        warnings.Add(new CartSyncWarning(
                            "OUT_OF_STOCK", "موجودی محصول تمام شده است",
                            item.ProductId, item.VariantId, item.SessionId));
                        continue;
                    }

                    if (variant.StockCount < allowedQuantity)
                    {
                        warnings.Add(new CartSyncWarning(
                            "QUANTITY_CLAMPED",
                            $"تعداد به {variant.StockCount} کاهش یافت",
                            item.ProductId, item.VariantId, item.SessionId));
                        allowedQuantity = variant.StockCount;
                    }
                }
                else
                {
                    if (product.StockCount <= 0)
                    {
                        warnings.Add(new CartSyncWarning(
                            "OUT_OF_STOCK", "موجودی محصول تمام شده است",
                            item.ProductId, item.VariantId, item.SessionId));
                        continue;
                    }

                    if (product.StockCount < allowedQuantity)
                    {
                        warnings.Add(new CartSyncWarning(
                            "QUANTITY_CLAMPED",
                            $"تعداد به {product.StockCount} کاهش یافت",
                            item.ProductId, item.VariantId, item.SessionId));
                        allowedQuantity = product.StockCount;
                    }
                }
            }
            else // SessionBased
            {
                if (item.SessionId.HasValue)
                {
                    var session = product.Sessions.FirstOrDefault(s => s.Id == item.SessionId.Value);
                    if (session is null || !session.IsAvailable)
                    {
                        warnings.Add(new CartSyncWarning(
                            "SESSION_REMOVED", "سانس موردنظر در دسترس نیست",
                            item.ProductId, item.VariantId, item.SessionId, item.UsageDate));
                        continue;
                    }

                    authoritativePrice += session.PriceAdjustment;

                    if (session.RemainingCapacity <= 0)
                    {
                        warnings.Add(new CartSyncWarning(
                            "OUT_OF_STOCK", "ظرفیت سانس تمام شده است",
                            item.ProductId, item.VariantId, item.SessionId, item.UsageDate));
                        continue;
                    }

                    if (session.RemainingCapacity < allowedQuantity)
                    {
                        warnings.Add(new CartSyncWarning(
                            "QUANTITY_CLAMPED",
                            $"تعداد به {session.RemainingCapacity} کاهش یافت",
                            item.ProductId, item.VariantId, item.SessionId, item.UsageDate));
                        allowedQuantity = session.RemainingCapacity;
                    }
                }
                else if (item.VariantId.HasValue)
                {
                    var variant = product.Variants.FirstOrDefault(v => v.Id == item.VariantId.Value);
                    if (variant is null)
                    {
                        warnings.Add(new CartSyncWarning(
                            "VARIANT_REMOVED", "تنوع محصول موجود نیست",
                            item.ProductId, item.VariantId, item.SessionId, item.UsageDate));
                        continue;
                    }

                    try
                    {
                        normalizedUsageDate = StoreVariantCapacityService.NormalizeAndValidateUsageDate(variant, item.UsageDate);
                        await StoreVariantCapacityService.EnsureCapacityAvailableAsync(
                            variant,
                            normalizedUsageDate,
                            allowedQuantity,
                            _mediator,
                            excludeOrderId: null,
                            cancellationToken);
                    }
                    catch (StoreDomainException ex)
                    {
                        warnings.Add(new CartSyncWarning(
                            ex.ErrorCode,
                            ex.Message,
                            item.ProductId, item.VariantId, item.SessionId, item.UsageDate));
                        continue;
                    }

                    authoritativePrice = variant.EffectivePriceMinor;
                }
                else
                {
                    warnings.Add(new CartSyncWarning(
                        "SESSION_REMOVED", "سانس محصول مشخص نشده است",
                        item.ProductId, item.VariantId, item.SessionId, item.UsageDate));
                    continue;
                }
            }

            // 4e. Price-changed warning (still proceed)
            if (authoritativePrice != item.UnitPriceMinor)
            {
                warnings.Add(new CartSyncWarning(
                    "PRICE_CHANGED", "قیمت محصول تغییر کرده است",
                    item.ProductId, item.VariantId, item.SessionId, normalizedUsageDate));
            }

            mergeSpecs.Add(new CartAggregate.MergeItemSpec(
                item.ShopId, item.ProductId, item.VariantId, item.SessionId, normalizedUsageDate,
                allowedQuantity, authoritativePrice));

            // After first accepted item we lock the dominantShopId
            dominantShopId ??= item.ShopId;
        }

        // 5. Merge into cart
        if (mergeSpecs.Count > 0)
        {
            cart.MergeItems(mergeSpecs);
            if (isNew)
                await _cartRepo.AddAsync(cart, cancellationToken);
            else
                await _cartRepo.UpdateAsync(cart, cancellationToken);
        }

        // 6. Project result via existing GetCartQuery handler
        var cartDto = await _mediator.Send(new GetCartQuery(request.UserId, request.ModuleId), cancellationToken);
        var response = new SyncCartResponse(cartDto, warnings.AsReadOnly());

        // Cache result for idempotency (24 h is enough)
        _cache.Set(cacheKey, response, TimeSpan.FromHours(24));

        return response;
    }
}
