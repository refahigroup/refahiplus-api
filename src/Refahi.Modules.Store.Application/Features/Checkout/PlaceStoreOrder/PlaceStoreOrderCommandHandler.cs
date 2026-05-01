using System.Text.Json;
using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Commands.Checkout;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

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
        var stockUpdates = new List<(Guid ProductId, Guid? VariantId, int Quantity)>();
        var sessionUpdates = new List<(Guid ProductId, Guid SessionId, int Quantity)>();

        // Cache agreement products per unique AgreementProductId
        var agreementProductCache = new Dictionary<Guid, Refahi.Modules.SupplyChain.Application.Contracts.Dtos.AgreementProductDto?>();

        foreach (var cartItem in cart.Items)
        {
            // Single-shop rule (via CartItem.ShopId)
            if (shopId.HasValue && cartItem.ShopId != shopId.Value)
                throw new StoreDomainException("تمامی محصولات باید از یک فروشگاه باشند", "MIXED_SHOP_ITEMS");
            shopId = cartItem.ShopId;

            var product = await _productRepo.GetByIdAsync(cartItem.ProductId, cancellationToken);
            if (product is null || product.IsDeleted)
                throw new StoreDomainException($"محصول '{cartItem.ProductId}' یافت نشد یا حذف شده است", "PRODUCT_NOT_FOUND");

            // Get AgreementProduct (cached)
            if (!agreementProductCache.TryGetValue(product.AgreementProductId, out var ap))
            {
                ap = await _mediator.Send(new GetAgreementProductByIdQuery(product.AgreementProductId), cancellationToken);
                agreementProductCache[product.AgreementProductId] = ap;
            }

            // CategoryCode via References
            string? categoryCode = null;
            if (ap?.CategoryId.HasValue == true)
            {
                var category = await _mediator.Send(new GetCategoryByIdQuery(ap.CategoryId.Value), cancellationToken);
                categoryCode = category?.CategoryCode;
            }

            // Build metadata from AgreementProduct
            string itemTitle;
            string metadataJson;

            // Use CartItem.ShopId for shop name lookup
            var shopForTitle = await _shopRepo.GetByIdAsync(cartItem.ShopId, cancellationToken);

            if (!cartItem.SessionId.HasValue) // StockBased
            {
                if (cartItem.VariantId.HasValue)
                {
                    var variant = product.Variants.FirstOrDefault(v => v.Id == cartItem.VariantId.Value)
                        ?? throw new StoreDomainException($"تنوع محصول '{product.Title}' یافت نشد", "VARIANT_NOT_FOUND");

                    if (!variant.IsAvailable || variant.StockCount < cartItem.Quantity)
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
                    stockUpdates.Add((product.Id, cartItem.VariantId, cartItem.Quantity));
                }
                else
                {
                    if (product.StockCount < cartItem.Quantity)
                        throw new StoreDomainException($"موجودی کافی برای '{product.Title}' وجود ندارد", "INSUFFICIENT_STOCK");

                    itemTitle = $"{product.Title} - {shopForTitle?.Name ?? string.Empty}";
                    stockUpdates.Add((product.Id, null, cartItem.Quantity));
                }

                metadataJson = JsonSerializer.Serialize(new
                {
                    shop_id = cartItem.ShopId.ToString(),
                    product_type = ap?.ProductType.ToString(),
                    sales_model = ap?.SalesModel.ToString(),
                    delivery_type = ap?.DeliveryType.ToString(),
                    variant_id = cartItem.VariantId?.ToString()
                });
            }
            else // SessionBased
            {
                var session = product.Sessions.FirstOrDefault(s => s.Id == cartItem.SessionId.Value)
                    ?? throw new StoreDomainException($"سانس محصول '{product.Title}' یافت نشد", "SESSION_NOT_FOUND");

                if (!session.IsAvailable || session.RemainingCapacity < cartItem.Quantity)
                    throw new StoreDomainException($"ظرفیت کافی برای سانس '{product.Title}' وجود ندارد", "INSUFFICIENT_CAPACITY");

                var sessionTitlePart = !string.IsNullOrWhiteSpace(session.Title) ? $" {session.Title}" : string.Empty;
                itemTitle = $"{product.Title}{sessionTitlePart} {session.Date:yyyy-MM-dd} - {shopForTitle?.Name ?? string.Empty}";

                metadataJson = JsonSerializer.Serialize(new
                {
                    shop_id = cartItem.ShopId.ToString(),
                    product_type = ap?.ProductType.ToString(),
                    sales_model = ap?.SalesModel.ToString(),
                    delivery_type = ap?.DeliveryType.ToString(),
                    session_id = cartItem.SessionId.Value.ToString(),
                    date = session.Date.ToString("yyyy-MM-dd"),
                    start_time = session.StartTime.ToString("HH:mm"),
                    end_time = session.EndTime.ToString("HH:mm")
                });

                sessionUpdates.Add((product.Id, cartItem.SessionId.Value, cartItem.Quantity));
            }

            orderItems.Add(new CreateOrderItemInput(
                Title: itemTitle,
                UnitPriceMinor: cartItem.UnitPriceMinor,
                Quantity: cartItem.Quantity,
                DiscountAmountMinor: 0,
                SourceItemId: cartItem.ProductId,
                CategoryCode: categoryCode ?? string.Empty,
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
