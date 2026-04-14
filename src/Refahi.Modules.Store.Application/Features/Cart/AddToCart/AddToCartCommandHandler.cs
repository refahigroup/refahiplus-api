using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using CartAggregate = Refahi.Modules.Store.Domain.Aggregates.Cart;

namespace Refahi.Modules.Store.Application.Features.Cart.AddToCart;

public class AddToCartCommandHandler : IRequestHandler<AddToCartCommand, AddToCartResponse>
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IProductSessionRepository _sessionRepo;

    public AddToCartCommandHandler(
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IProductSessionRepository sessionRepo)
    {
        _cartRepo = cartRepo;
        _productRepo = productRepo;
        _sessionRepo = sessionRepo;
    }

    public async Task<AddToCartResponse> Handle(AddToCartCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        if (product.IsDeleted)
            throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        long unitPrice = product.EffectivePriceMinor;

        if (product.SalesModel == SalesModel.StockBased)
        {
            if (request.VariantId.HasValue)
            {
                var variant = product.Variants.FirstOrDefault(v => v.Id == request.VariantId.Value)
                    ?? throw new StoreDomainException("تنوع محصول یافت نشد", "VARIANT_NOT_FOUND");

                if (!variant.IsAvailable)
                    throw new StoreDomainException("این تنوع محصول موجود نیست", "VARIANT_NOT_AVAILABLE");

                if (variant.StockCount < request.Quantity)
                    throw new StoreDomainException("موجودی کافی نیست", "INSUFFICIENT_STOCK");

                unitPrice += variant.PriceAdjustment;
            }
            else
            {
                if (product.StockCount < request.Quantity)
                    throw new StoreDomainException("موجودی کافی نیست", "INSUFFICIENT_STOCK");
            }
        }
        else // SessionBased
        {
            if (!request.SessionId.HasValue)
                throw new StoreDomainException("برای محصولات سانسی، انتخاب سانس الزامی است", "SESSION_REQUIRED");

            var session = product.Sessions.FirstOrDefault(s => s.Id == request.SessionId.Value)
                ?? throw new StoreDomainException("سانس یافت نشد", "SESSION_NOT_FOUND");

            if (!session.IsAvailable)
                throw new StoreDomainException("این سانس در دسترس نیست", "SESSION_NOT_AVAILABLE");

            if (session.RemainingCapacity < request.Quantity)
                throw new StoreDomainException("ظرفیت کافی نیست", "INSUFFICIENT_CAPACITY");

            unitPrice += session.PriceAdjustment;
        }

        var cart = await _cartRepo.GetByUserIdAsync(request.UserId, cancellationToken);

        if (cart is null)
        {
            cart = CartAggregate.Create(request.UserId);
            cart.AddItem(request.ProductId, request.VariantId, request.SessionId, request.Quantity, unitPrice);
            await _cartRepo.AddAsync(cart, cancellationToken);
        }
        else
        {
            cart.AddItem(request.ProductId, request.VariantId, request.SessionId, request.Quantity, unitPrice);
            await _cartRepo.UpdateAsync(cart, cancellationToken);
        }

        return new AddToCartResponse(cart.Id, cart.Items.Sum(i => i.Quantity));
    }
}
