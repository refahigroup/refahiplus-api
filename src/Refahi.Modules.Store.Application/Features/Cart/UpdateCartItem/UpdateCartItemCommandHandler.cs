using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.Store.Application.Features.Cart.UpdateCartItem;

public class UpdateCartItemCommandHandler : IRequestHandler<UpdateCartItemCommand, UpdateCartItemResponse>
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IMediator _mediator;

    public UpdateCartItemCommandHandler(
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IMediator mediator)
    {
        _cartRepo = cartRepo;
        _productRepo = productRepo;
        _mediator = mediator;
    }

    public async Task<UpdateCartItemResponse> Handle(UpdateCartItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await _cartRepo.GetByUserAndModuleIdAsync(request.UserId, request.ModuleId, cancellationToken)
            ?? throw new StoreDomainException("سبد خرید یافت نشد", "CART_NOT_FOUND");

        var item = cart.Items.FirstOrDefault(i => i.Id == request.CartItemId)
            ?? throw new StoreDomainException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");

        await ValidateRequestedQuantityAsync(item, request.Quantity, cancellationToken);

        cart.UpdateItemQuantity(request.CartItemId, request.Quantity);

        await _cartRepo.UpdateAsync(cart, cancellationToken);

        return new UpdateCartItemResponse(item.Id, request.Quantity);
    }

    private async Task ValidateRequestedQuantityAsync(
        Refahi.Modules.Store.Domain.Entities.CartItem item,
        int quantity,
        CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(item.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        if (product.IsDeleted)
            throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        var agreementProduct = await _mediator.Send(
            new GetAgreementProductByIdQuery(product.AgreementProductId),
            cancellationToken)
            ?? throw new StoreDomainException("اطلاعات محصول یافت نشد", "AGREEMENT_PRODUCT_NOT_FOUND");

        var salesModel = (SalesModel)agreementProduct.SalesModel;
        if (salesModel == SalesModel.StockBased)
        {
            if (item.VariantId.HasValue)
            {
                var variant = product.Variants.FirstOrDefault(v => v.Id == item.VariantId.Value)
                    ?? throw new StoreDomainException("تنوع محصول یافت نشد", "VARIANT_NOT_FOUND");

                if (!variant.HasLegacyStockAvailable(quantity))
                    throw new StoreDomainException("موجودی کافی نیست", "INSUFFICIENT_STOCK");
            }
            else if (product.StockCount < quantity)
            {
                throw new StoreDomainException("موجودی کافی نیست", "INSUFFICIENT_STOCK");
            }

            return;
        }

        if (item.SessionId.HasValue)
        {
            var session = product.Sessions.FirstOrDefault(s => s.Id == item.SessionId.Value)
                ?? throw new StoreDomainException("سانس یافت نشد", "SESSION_NOT_FOUND");

            if (!session.IsAvailable || session.RemainingCapacity < quantity)
                throw new StoreDomainException("ظرفیت کافی نیست", "INSUFFICIENT_CAPACITY");

            return;
        }

        if (!item.VariantId.HasValue)
            throw new StoreDomainException("برای محصولات سانسی، انتخاب سانس یا خدمت الزامی است", "SESSION_REQUIRED");

        var accessVariant = product.Variants.FirstOrDefault(v => v.Id == item.VariantId.Value)
            ?? throw new StoreDomainException("تنوع محصول یافت نشد", "VARIANT_NOT_FOUND");

        var normalizedUsageDate = StoreVariantCapacityService.NormalizeAndValidateUsageDate(accessVariant, item.UsageDate);
        await StoreVariantCapacityService.EnsureCapacityAvailableAsync(
            accessVariant,
            normalizedUsageDate,
            quantity,
            _mediator,
            excludeOrderId: null,
            cancellationToken);
    }
}
