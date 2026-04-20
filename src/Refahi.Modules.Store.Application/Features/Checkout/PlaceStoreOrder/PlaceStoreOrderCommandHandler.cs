using System.Text.Json;
using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Store.Application.Contracts.Commands.Checkout;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Checkout.PlaceStoreOrder;

public class PlaceStoreOrderCommandHandler : IRequestHandler<PlaceStoreOrderCommand, PlaceStoreOrderResponse>
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopRepository _shopRepo;
    private readonly IProductSessionRepository _sessionRepo;
    private readonly IMediator _mediator;

    public PlaceStoreOrderCommandHandler(
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IShopRepository shopRepo,
        IProductSessionRepository sessionRepo,
        IMediator mediator)
    {
        _cartRepo = cartRepo;
        _productRepo = productRepo;
        _shopRepo = shopRepo;
        _sessionRepo = sessionRepo;
        _mediator = mediator;
    }

    public async Task<PlaceStoreOrderResponse> Handle(PlaceStoreOrderCommand request, CancellationToken cancellationToken)
    {
        // STEP 1: Load cart
        var cart = await _cartRepo.GetByUserAndModuleIdAsync(request.UserId, request.ModuleId, cancellationToken);

        if (cart is null || !cart.Items.Any())
            throw new StoreDomainException("سبد خرید خالی است", "CART_EMPTY");

        // STEP 2: Validate all products and build order items
        Guid? shopId = null;
        var orderItems = new List<CreateOrderItemInput>();

        // Track items for stock/session updates after payment
        var stockUpdates = new List<(Guid ProductId, Guid? VariantId, int Quantity)>();
        var sessionUpdates = new List<(Guid ProductId, Guid SessionId, int Quantity)>();

        foreach (var cartItem in cart.Items)
        {
            var product = await _productRepo.GetByIdAsync(cartItem.ProductId, cancellationToken);

            if (product is null || product.IsDeleted)
                throw new StoreDomainException($"محصول '{cartItem.ProductId}' یافت نشد یا حذف شده است", "PRODUCT_NOT_FOUND");

            // Single-shop rule
            if (shopId.HasValue && product.ShopId != shopId.Value)
                throw new StoreDomainException("تمامی محصولات باید از یک فروشگاه باشند", "MIXED_SHOP_ITEMS");

            shopId = product.ShopId;

            long unitPrice = product.EffectivePriceMinor;
            string itemTitle;
            string metadataJson;

            if (product.SalesModel == SalesModel.StockBased)
            {
                // Validate stock
                if (cartItem.VariantId.HasValue)
                {
                    var variant = product.Variants.FirstOrDefault(v => v.Id == cartItem.VariantId.Value)
                        ?? throw new StoreDomainException($"تنوع محصول '{product.Title}' یافت نشد", "VARIANT_NOT_FOUND");

                    if (!variant.IsAvailable || variant.StockCount < cartItem.Quantity)
                        throw new StoreDomainException($"موجودی کافی برای '{product.Title}' وجود ندارد", "INSUFFICIENT_STOCK");

                    unitPrice = variant.EffectivePriceMinor;

                    var shopForTitle = shopId.HasValue ? await _shopRepo.GetByIdAsync(shopId.Value, cancellationToken) : null;
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
                        shop_id = product.ShopId.ToString(),
                        product_type = product.ProductType.ToString(),
                        sales_model = product.SalesModel.ToString(),
                        delivery_type = product.DeliveryType.ToString(),
                        variant_id = cartItem.VariantId.Value.ToString()
                    });

                    stockUpdates.Add((product.Id, cartItem.VariantId, cartItem.Quantity));
                }
                else
                {
                    if (product.StockCount < cartItem.Quantity)
                        throw new StoreDomainException($"موجودی کافی برای '{product.Title}' وجود ندارد", "INSUFFICIENT_STOCK");

                    var shopForTitle = shopId.HasValue ? await _shopRepo.GetByIdAsync(shopId.Value, cancellationToken) : null;
                    itemTitle = $"{product.Title} - {shopForTitle?.Name ?? string.Empty}";

                    metadataJson = JsonSerializer.Serialize(new
                    {
                        shop_id = product.ShopId.ToString(),
                        product_type = product.ProductType.ToString(),
                        sales_model = product.SalesModel.ToString(),
                        delivery_type = product.DeliveryType.ToString()
                    });

                    stockUpdates.Add((product.Id, null, cartItem.Quantity));
                }
            }
            else // SessionBased
            {
                if (!cartItem.SessionId.HasValue)
                    throw new StoreDomainException($"سانس برای محصول '{product.Title}' مشخص نشده است", "SESSION_REQUIRED");

                var session = product.Sessions.FirstOrDefault(s => s.Id == cartItem.SessionId.Value)
                    ?? throw new StoreDomainException($"سانس محصول '{product.Title}' یافت نشد", "SESSION_NOT_FOUND");

                if (!session.IsAvailable || session.RemainingCapacity < cartItem.Quantity)
                    throw new StoreDomainException($"ظرفیت کافی برای سانس '{product.Title}' وجود ندارد", "INSUFFICIENT_CAPACITY");

                unitPrice += session.PriceAdjustment;

                var shopForTitle = shopId.HasValue ? await _shopRepo.GetByIdAsync(shopId.Value, cancellationToken) : null;
                var sessionTitlePart = !string.IsNullOrWhiteSpace(session.Title) ? $" {session.Title}" : string.Empty;
                itemTitle = $"{product.Title}{sessionTitlePart} {session.Date:yyyy-MM-dd} - {shopForTitle?.Name ?? string.Empty}";

                metadataJson = JsonSerializer.Serialize(new
                {
                    shop_id = product.ShopId.ToString(),
                    product_type = product.ProductType.ToString(),
                    sales_model = product.SalesModel.ToString(),
                    delivery_type = product.DeliveryType.ToString(),
                    session_id = cartItem.SessionId.Value.ToString(),
                    date = session.Date.ToString("yyyy-MM-dd"),
                    start_time = session.StartTime.ToString("HH:mm"),
                    end_time = session.EndTime.ToString("HH:mm")
                });

                sessionUpdates.Add((product.Id, cartItem.SessionId.Value, cartItem.Quantity));
            }

            orderItems.Add(new CreateOrderItemInput(
                Title: itemTitle,
                UnitPriceMinor: unitPrice,
                Quantity: cartItem.Quantity,
                DiscountAmountMinor: 0,
                SourceItemId: cartItem.ProductId,
                CategoryCode: product.CategoryCode,
                Tags: null,
                MetadataJson: metadataJson));
        }

        // STEP 3: Create order via Orders module
        var createOrderCommand = new CreateOrderCommand(
            UserId: request.UserId,
            SourceModule: "Store",
            SourceReferenceId: shopId!.Value,
            Items: orderItems,
            IdempotencyKey: $"store-order-{request.IdempotencyKey}");

        var orderResult = await _mediator.Send(createOrderCommand, cancellationToken);

        // STEP 4: Pay order via Orders module (calls Wallet)
        var payCommand = new PayOrderCommand(
            OrderId: orderResult.OrderId,
            Allocations: request.WalletAllocations
                .Select(w => new WalletAllocationInput(w.WalletId, w.AmountMinor))
                .ToList(),
            IdempotencyKey: $"store-pay-{request.IdempotencyKey}");

        var payResult = await _mediator.Send(payCommand, cancellationToken);

        // STEP 5: Decrease stock/capacity ONLY after payment success
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
                    // Entity was detached by repo; re-fetch fresh from DB before retrying
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

        // STEP 6: Clear cart
        cart.Clear();
        await _cartRepo.UpdateAsync(cart, cancellationToken);

        // STEP 7: Return response
        return new PlaceStoreOrderResponse(
            OrderId: orderResult.OrderId,
            OrderNumber: orderResult.OrderNumber,
            FinalAmountMinor: orderResult.FinalAmountMinor,
            Status: payResult.Status);
    }
}
