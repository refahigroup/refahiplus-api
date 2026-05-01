using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;
using CartAggregate = Refahi.Modules.Store.Domain.Aggregates.Cart;

namespace Refahi.Modules.Store.Application.Features.Cart.AddToCart;

public class AddToCartCommandHandler : IRequestHandler<AddToCartCommand, AddToCartResponse>
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IProductSessionRepository _sessionRepo;
    private readonly IMediator _mediator;

    public AddToCartCommandHandler(
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IShopProductRepository shopProductRepo,
        IProductSessionRepository sessionRepo,
        IMediator mediator)
    {
        _cartRepo = cartRepo;
        _productRepo = productRepo;
        _shopProductRepo = shopProductRepo;
        _sessionRepo = sessionRepo;
        _mediator = mediator;
    }

    public async Task<AddToCartResponse> Handle(AddToCartCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        if (product.IsDeleted)
            throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        // Validate product is linked to this shop and currently active
        var shopProduct = await _shopProductRepo.GetAsync(request.ShopId, request.ProductId, cancellationToken);
        if (shopProduct is null || !shopProduct.IsActive)
            throw new StoreDomainException("این محصول در فروشگاه مورد نظر موجود نیست", "PRODUCT_NOT_IN_SHOP");

        // Get sales model from AgreementProduct; price comes from ShopProduct
        var ap = await _mediator.Send(new GetAgreementProductByIdQuery(product.AgreementProductId), cancellationToken)
            ?? throw new StoreDomainException("اطلاعات محصول یافت نشد", "AGREEMENT_PRODUCT_NOT_FOUND");

        long unitPrice = shopProduct.DiscountedPrice > 0 ? shopProduct.DiscountedPrice : shopProduct.Price;
        var salesModel = (SalesModel)ap.SalesModel;

        if (salesModel == SalesModel.StockBased)
        {
            if (request.VariantId.HasValue)
            {
                var variant = product.Variants.FirstOrDefault(v => v.Id == request.VariantId.Value)
                    ?? throw new StoreDomainException("تنوع محصول یافت نشد", "VARIANT_NOT_FOUND");

                if (!variant.IsAvailable)
                    throw new StoreDomainException("این تنوع محصول موجود نیست", "VARIANT_NOT_AVAILABLE");

                if (variant.StockCount < request.Quantity)
                    throw new StoreDomainException("موجودی کافی نیست", "INSUFFICIENT_STOCK");

                // Use variant price if set, otherwise use agreement product price
                unitPrice = variant.DiscountedPriceMinor ?? variant.PriceMinor;
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

        var cart = await _cartRepo.GetByUserAndModuleIdAsync(request.UserId, request.ModuleId, cancellationToken);

        if (cart is null)
        {
            cart = CartAggregate.Create(request.UserId, request.ModuleId);
            cart.AddItem(request.ShopId, request.ProductId, request.VariantId, request.SessionId, request.Quantity, unitPrice);
            await _cartRepo.AddAsync(cart, cancellationToken);
        }
        else
        {
            cart.AddItem(request.ShopId, request.ProductId, request.VariantId, request.SessionId, request.Quantity, unitPrice);
            await _cartRepo.UpdateAsync(cart, cancellationToken);
        }

        return new AddToCartResponse(cart.Id, cart.Items.Sum(i => i.Quantity));
    }
}
