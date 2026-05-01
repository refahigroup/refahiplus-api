using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.AddProductToShop;

public class AddProductToShopCommandHandler : IRequestHandler<AddProductToShopCommand, AddProductToShopResponse>
{
    private readonly IShopRepository _shopRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopProductRepository _shopProductRepo;

    public AddProductToShopCommandHandler(
        IShopRepository shopRepo,
        IProductRepository productRepo,
        IShopProductRepository shopProductRepo)
    {
        _shopRepo = shopRepo;
        _productRepo = productRepo;
        _shopProductRepo = shopProductRepo;
    }

    public async Task<AddProductToShopResponse> Handle(AddProductToShopCommand request, CancellationToken cancellationToken)
    {
        var shop = await _shopRepo.GetByIdAsync(request.ShopId, cancellationToken)
            ?? throw new StoreDomainException("فروشگاه یافت نشد", "SHOP_NOT_FOUND");

        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        if (product.IsDeleted)
            throw new StoreDomainException("محصول حذف شده است", "PRODUCT_DELETED");

        // Check if already exists (non-deleted)
        var existing = await _shopProductRepo.GetAsync(request.ShopId, request.ProductId, cancellationToken);
        if (existing is not null && !existing.IsDeleted)
            throw new StoreDomainException("این محصول قبلاً به فروشگاه اضافه شده است", "SHOP_PRODUCT_EXISTS");

        var shopProduct = ShopProduct.Create(request.ShopId, request.ProductId, request.Price, request.DiscountedPrice, request.Description);
        await _shopProductRepo.AddAsync(shopProduct, cancellationToken);

        return new AddProductToShopResponse(shopProduct.Id);
    }
}
