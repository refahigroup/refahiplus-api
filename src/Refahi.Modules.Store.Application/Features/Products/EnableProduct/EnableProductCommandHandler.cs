using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.EnableProduct;

public class EnableProductCommandHandler : IRequestHandler<EnableProductCommand, EnableProductResponse>
{
    private readonly IProductRepository _productRepo;

    public EnableProductCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<EnableProductResponse> Handle(
        EnableProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdForAdminAsync(request.Id, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        product.Restore();

        await _productRepo.UpdateAsync(product, cancellationToken);

        return new EnableProductResponse(product.Id, product.IsDeleted);
    }
}
