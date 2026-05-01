using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.SetMainProductImage;

public class SetMainProductImageCommandHandler : IRequestHandler<SetMainProductImageCommand, Unit>
{
    private readonly IProductRepository _productRepo;

    public SetMainProductImageCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<Unit> Handle(SetMainProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        product.SetMainImage(request.ImageId);

        await _productRepo.UpdateAsync(product, cancellationToken);
        return Unit.Value;
    }
}
