using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.AddProductImage;

public class AddProductImageCommandHandler : IRequestHandler<AddProductImageCommand, AddProductImageResponse>
{
    private readonly IProductRepository _productRepo;

    public AddProductImageCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<AddProductImageResponse> Handle(AddProductImageCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        product.AddImage(request.ImageUrl, request.IsMain, request.SortOrder);

        await _productRepo.UpdateAsync(product, cancellationToken);

        var addedImage = product.Images
            .Where(i => i.ImageUrl == request.ImageUrl && i.SortOrder == request.SortOrder)
            .OrderByDescending(i => i.Id)
            .First();
        return new AddProductImageResponse(addedImage.Id);
    }
}
