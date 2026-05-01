using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.UpdateProduct;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, UpdateProductResponse>
{
    private readonly IProductRepository _productRepo;

    public UpdateProductCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<UpdateProductResponse> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        product.UpdateInfo(request.Title, request.Description, request.IsAvailable);

        await _productRepo.UpdateAsync(product, cancellationToken);

        return new UpdateProductResponse(product.Id, product.Title);
    }
}
