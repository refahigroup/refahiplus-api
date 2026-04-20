using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.DisableProduct;

public class DisableProductCommandHandler : IRequestHandler<DisableProductCommand, DisableProductResponse>
{
    private readonly IProductRepository _productRepo;

    public DisableProductCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<DisableProductResponse> Handle(
        DisableProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        product.SoftDelete();

        await _productRepo.UpdateAsync(product, cancellationToken);

        return new DisableProductResponse(product.Id, product.IsDeleted);
    }
}
