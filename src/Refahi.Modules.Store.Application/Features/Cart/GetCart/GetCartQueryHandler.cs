using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Cart;
using Refahi.Modules.Store.Application.Contracts.Queries.Cart;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Cart.GetCart;

public class GetCartQueryHandler : IRequestHandler<GetCartQuery, CartDto>
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IProductSessionRepository _sessionRepo;
    private readonly IShopRepository _shopRepo;
    private readonly IShopProductRepository _shopProductRepo;

    public GetCartQueryHandler(
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IProductSessionRepository sessionRepo,
        IShopRepository shopRepo,
        IShopProductRepository shopProductRepo)
    {
        _cartRepo = cartRepo;
        _productRepo = productRepo;
        _sessionRepo = sessionRepo;
        _shopRepo = shopRepo;
        _shopProductRepo = shopProductRepo;
    }

    public async Task<CartDto> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var cart = await _cartRepo.GetByUserAndModuleIdAsync(request.UserId, request.ModuleId, cancellationToken);

        if (cart is null)
            return new CartDto(Guid.Empty, new List<CartItemDto>(), 0, 0, 0, 0);

        var itemDtos = new List<CartItemDto>();

        // Cache shop names per ShopId
        var shopNameCache = new Dictionary<Guid, string?>();

        foreach (var item in cart.Items)
        {
            // Shop name (cached)
            if (!shopNameCache.TryGetValue(item.ShopId, out var shopName))
            {
                var shop = await _shopRepo.GetByIdAsync(item.ShopId, cancellationToken);
                shopName = shop?.Name;
                shopNameCache[item.ShopId] = shopName;
            }

            var product = await _productRepo.GetByIdAsync(item.ProductId, cancellationToken);

            if (product is null || product.IsDeleted)
            {
                itemDtos.Add(new CartItemDto(
                    Id: item.Id,
                    ShopId: item.ShopId,
                    ShopName: shopName,
                    ProductId: item.ProductId,
                    ProductTitle: "محصول حذف شده",
                    ProductImageUrl: null,
                    VariantId: item.VariantId,
                    VariantLabel: null,
                    SessionId: item.SessionId,
                    SessionLabel: null,
                    Quantity: item.Quantity,
                    UnitPriceMinor: item.UnitPriceMinor,
                    OriginalUnitPriceMinor: item.UnitPriceMinor,
                    DiscountPercent: 0,
                    TotalPriceMinor: item.UnitPriceMinor * item.Quantity,
                    IsAvailable: false));
                continue;
            }

            var mainImage = product.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                         ?? product.Images.FirstOrDefault()?.ImageUrl;

            string? variantLabel = null;
            bool isAvailable = product.IsAvailable;

            // محاسبه‌ی OriginalUnitPriceMinor
            long originalUnitPrice = item.UnitPriceMinor;

            if (item.VariantId.HasValue)
            {
                var variant = product.Variants.FirstOrDefault(v => v.Id == item.VariantId.Value);
                if (variant is not null)
                {
                    variantLabel = !string.IsNullOrWhiteSpace(variant.SKU)
                        ? variant.SKU
                        : string.Join(" / ", variant.Combinations.Select(c =>
                        {
                            var attr = product.VariantAttributes.FirstOrDefault(a => a.Id == c.VariantAttributeId);
                            var val = attr?.Values.FirstOrDefault(v => v.Id == c.VariantAttributeValueId);
                            return val?.Value ?? string.Empty;
                        }).Where(s => !string.IsNullOrEmpty(s)));
                    isAvailable = variant.IsAvailable && variant.StockCount >= item.Quantity;
                    originalUnitPrice = variant.PriceMinor;
                }
                else
                {
                    isAvailable = false;
                }
            }
            else
            {
                // بدون variant — قیمت اصلی از ShopProduct
                var shopProduct = await _shopProductRepo.GetAsync(item.ShopId, item.ProductId, cancellationToken);
                if (shopProduct is not null)
                {
                    originalUnitPrice = shopProduct.Price;
                }
            }

            string? sessionLabel = null;
            if (item.SessionId.HasValue)
            {
                var session = product.Sessions.FirstOrDefault(s => s.Id == item.SessionId.Value);
                if (session is not null)
                {
                    var titlePart = !string.IsNullOrWhiteSpace(session.Title) ? session.Title + " " : string.Empty;
                    sessionLabel = $"{titlePart}{session.Date:yyyy-MM-dd} {session.StartTime:HH:mm}-{session.EndTime:HH:mm}";
                    isAvailable = session.IsAvailable && session.RemainingCapacity >= item.Quantity;
                }
                else
                {
                    isAvailable = false;
                }
            }

            // محاسبه DiscountPercent
            int discountPercent = 0;
            if (originalUnitPrice > 0 && originalUnitPrice > item.UnitPriceMinor)
            {
                discountPercent = (int)Math.Round(
                    (1.0 - (double)item.UnitPriceMinor / originalUnitPrice) * 100);
            }
            else
            {
                // اگر OriginalPrice کمتر یا مساوی UnitPrice است، تخفیف نداریم
                originalUnitPrice = item.UnitPriceMinor;
            }

            itemDtos.Add(new CartItemDto(
                Id: item.Id,
                ShopId: item.ShopId,
                ShopName: shopName,
                ProductId: item.ProductId,
                ProductTitle: product.Title,
                ProductImageUrl: mainImage,
                VariantId: item.VariantId,
                VariantLabel: variantLabel,
                SessionId: item.SessionId,
                SessionLabel: sessionLabel,
                Quantity: item.Quantity,
                UnitPriceMinor: item.UnitPriceMinor,
                OriginalUnitPriceMinor: originalUnitPrice,
                DiscountPercent: discountPercent,
                TotalPriceMinor: item.UnitPriceMinor * item.Quantity,
                IsAvailable: isAvailable));
        }

        var totalMinor = itemDtos.Sum(i => i.TotalPriceMinor);
        var originalTotalMinor = itemDtos.Sum(i => i.OriginalUnitPriceMinor * i.Quantity);
        var discountTotalMinor = originalTotalMinor - totalMinor;
        if (discountTotalMinor < 0) discountTotalMinor = 0;

        return new CartDto(
            CartId: cart.Id,
            Items: itemDtos,
            TotalMinor: totalMinor,
            OriginalTotalMinor: originalTotalMinor,
            DiscountTotalMinor: discountTotalMinor,
            TotalItems: cart.Items.Sum(i => i.Quantity));
    }
}
