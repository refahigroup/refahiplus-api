using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.AddProductToShop;

public class AddProductToShopCommandHandler : IRequestHandler<AddProductToShopCommand, AddProductToShopResponse>
{
    private readonly IShopRepository _shopRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IMediator _mediator;

    public AddProductToShopCommandHandler(
        IShopRepository shopRepo,
        IProductRepository productRepo,
        IShopProductRepository shopProductRepo,
        IMediator mediator)
    {
        _shopRepo = shopRepo;
        _productRepo = productRepo;
        _shopProductRepo = shopProductRepo;
        _mediator = mediator;
    }

    public async Task<AddProductToShopResponse> Handle(AddProductToShopCommand request, CancellationToken cancellationToken)
    {
        var shop = await _shopRepo.GetByIdAsync(request.ShopId, cancellationToken)
            ?? throw new StoreDomainException("فروشگاه یافت نشد", "SHOP_NOT_FOUND");

        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        if (product.IsDeleted)
            throw new StoreDomainException("محصول حذف شده است", "PRODUCT_DELETED");

        var agreementProduct = await _mediator.Send(
            new GetAgreementProductByIdQuery(product.AgreementProductId), cancellationToken)
            ?? throw new StoreDomainException("محصول قرارداد یافت نشد", "AGREEMENT_PRODUCT_NOT_FOUND");
        var isManual = agreementProduct.PricingMode == 2;
        if (isManual)
        {
            if (agreementProduct.DeliveryType != 3 || agreementProduct.SalesModel != 3
                || shop.ShopType != ShopType.Physical || shop.Status != ShopStatus.Active
                || agreementProduct.SupplierId != shop.SupplierId)
                throw new StoreDomainException("محصول حضوری با فروشگاه انتخاب‌شده سازگار نیست", "INVALID_MANUAL_SHOP_PRODUCT");
            if (request.Price != 0 || request.DiscountedPrice != 0)
                throw new StoreDomainException("قیمت محصول حضوری باید صفر ذخیره شود", "INVALID_MANUAL_PRICE");
        }
        else if (request.Price <= 0 || request.DiscountedPrice <= 0)
        {
            throw new StoreDomainException("قیمت محصول باید بزرگ‌تر از صفر باشد", "INVALID_FIXED_PRICE");
        }

        // Check if already exists (non-deleted)
        var existing = await _shopProductRepo.GetAsync(request.ShopId, request.ProductId, cancellationToken);
        if (existing is not null && !existing.IsDeleted)
            throw new StoreDomainException("این محصول قبلاً به فروشگاه اضافه شده است", "SHOP_PRODUCT_EXISTS");

        var shopProduct = ShopProduct.Create(request.ShopId, request.ProductId, request.Price, request.DiscountedPrice, request.Description);
        await _shopProductRepo.AddAsync(shopProduct, cancellationToken);

        return new AddProductToShopResponse(shopProduct.Id);
    }
}
