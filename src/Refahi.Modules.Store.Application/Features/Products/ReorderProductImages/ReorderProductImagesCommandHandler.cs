using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.ReorderProductImages;

public class ReorderProductImagesCommandHandler : IRequestHandler<ReorderProductImagesCommand, Unit>
{
    private readonly IProductRepository _productRepo;

    public ReorderProductImagesCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<Unit> Handle(ReorderProductImagesCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        var map = request.Items.Select(i => (i.ImageId, i.SortOrder));
        product.ReorderImages(map);

        await _productRepo.UpdateAsync(product, cancellationToken);
        return Unit.Value;
    }
}
