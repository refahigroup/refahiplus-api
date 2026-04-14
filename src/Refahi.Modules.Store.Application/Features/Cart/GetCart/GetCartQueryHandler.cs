using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Cart;
using Refahi.Modules.Store.Application.Contracts.Queries.Cart;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Cart.GetCart;

public class GetCartQueryHandler : IRequestHandler<GetCartQuery, CartDto>
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IProductSessionRepository _sessionRepo;

    public GetCartQueryHandler(
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IProductSessionRepository sessionRepo)
    {
        _cartRepo = cartRepo;
        _productRepo = productRepo;
        _sessionRepo = sessionRepo;
    }

    public async Task<CartDto> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var cart = await _cartRepo.GetByUserIdAsync(request.UserId, cancellationToken);

        if (cart is null)
            return new CartDto(Guid.Empty, new List<CartItemDto>(), 0, 0);

        var itemDtos = new List<CartItemDto>();

        foreach (var item in cart.Items)
        {
            var product = await _productRepo.GetByIdAsync(item.ProductId, cancellationToken);

            if (product is null || product.IsDeleted)
            {
                itemDtos.Add(new CartItemDto(
                    item.Id, item.ProductId,
                    "محصول حذف شده", null,
                    item.VariantId, null,
                    item.SessionId, null,
                    item.Quantity, item.UnitPriceMinor,
                    item.UnitPriceMinor * item.Quantity,
                    IsAvailable: false));
                continue;
            }

            var mainImage = product.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                         ?? product.Images.FirstOrDefault()?.ImageUrl;

            string? variantLabel = null;
            bool isAvailable = product.IsAvailable;

            if (item.VariantId.HasValue)
            {
                var variant = product.Variants.FirstOrDefault(v => v.Id == item.VariantId.Value);
                if (variant is not null)
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(variant.Size))
                        parts.Add($"سایز {variant.Size}");
                    if (!string.IsNullOrWhiteSpace(variant.Color))
                        parts.Add(variant.Color);
                    variantLabel = parts.Count > 0 ? string.Join(" - ", parts) : null;
                    isAvailable = variant.IsAvailable && variant.StockCount >= item.Quantity;
                }
                else
                {
                    isAvailable = false;
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

            itemDtos.Add(new CartItemDto(
                item.Id, item.ProductId,
                product.Title, mainImage,
                item.VariantId, variantLabel,
                item.SessionId, sessionLabel,
                item.Quantity, item.UnitPriceMinor,
                item.UnitPriceMinor * item.Quantity,
                isAvailable));
        }

        return new CartDto(
            cart.Id,
            itemDtos,
            cart.TotalMinor,
            cart.Items.Sum(i => i.Quantity));
    }
}
