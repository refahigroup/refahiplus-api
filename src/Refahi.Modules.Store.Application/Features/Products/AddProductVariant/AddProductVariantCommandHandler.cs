using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.AddProductVariant;

public class AddProductVariantCommandHandler : IRequestHandler<AddProductVariantCommand, AddProductVariantResponse>
{
    private readonly IProductRepository _productRepo;

    public AddProductVariantCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<AddProductVariantResponse> Handle(AddProductVariantCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        product.AddVariant(request.Size, request.Color, request.ColorHex,
            request.ImageUrl, request.StockCount, request.PriceAdjustment);

        await _productRepo.UpdateAsync(product, cancellationToken);

        var addedVariant = product.Variants.Last();
        return new AddProductVariantResponse(addedVariant.Id);
    }
}
