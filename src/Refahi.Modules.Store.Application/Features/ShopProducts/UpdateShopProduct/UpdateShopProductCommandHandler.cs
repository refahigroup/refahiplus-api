using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.UpdateShopProduct;

public class UpdateShopProductCommandHandler : IRequestHandler<UpdateShopProductCommand, Unit>
{
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IProductRepository _productRepo;
    private readonly IMediator _mediator;

    public UpdateShopProductCommandHandler(
        IShopProductRepository shopProductRepo,
        IProductRepository productRepo,
        IMediator mediator)
    {
        _shopProductRepo = shopProductRepo;
        _productRepo = productRepo;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(UpdateShopProductCommand request, CancellationToken cancellationToken)
    {
        var shopProduct = await _shopProductRepo.GetAsync(request.ShopId, request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول در این فروشگاه یافت نشد", "SHOP_PRODUCT_NOT_FOUND");

        if (shopProduct.IsDeleted)
            throw new StoreDomainException("محصول در این فروشگاه حذف شده است", "SHOP_PRODUCT_DELETED");

        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");
        var agreementProduct = await _mediator.Send(
            new GetAgreementProductByIdQuery(product.AgreementProductId), cancellationToken)
            ?? throw new StoreDomainException("محصول قرارداد یافت نشد", "AGREEMENT_PRODUCT_NOT_FOUND");
        var isManual = agreementProduct.PricingMode == (short)PricingMode.Manual;
        if (isManual)
        {
            if (request.Price != 0 || request.DiscountedPrice != 0)
                throw new StoreDomainException("قیمت محصول حضوری باید صفر ذخیره شود", "INVALID_MANUAL_PRICE");
        }
        else if (request.Price <= 0 || request.DiscountedPrice <= 0)
        {
            throw new StoreDomainException("قیمت محصول باید بزرگ‌تر از صفر باشد", "INVALID_FIXED_PRICE");
        }

        if (isManual)
            shopProduct.UpdateDetailsWithManualPricing(request.Description);
        else
            shopProduct.UpdateDetails(request.Price, request.DiscountedPrice, request.Description);
        await _shopProductRepo.UpdateAsync(shopProduct, cancellationToken);
        return Unit.Value;
    }
}
