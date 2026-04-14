using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.AddProductSpecification;

public class AddProductSpecificationCommandHandler : IRequestHandler<AddProductSpecificationCommand, AddProductSpecificationResponse>
{
    private readonly IProductRepository _productRepo;

    public AddProductSpecificationCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<AddProductSpecificationResponse> Handle(AddProductSpecificationCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        product.AddSpecification(request.Key, request.Value, request.SortOrder);

        await _productRepo.UpdateAsync(product, cancellationToken);

        var addedSpec = product.Specifications
            .Where(s => s.Key == request.Key && s.SortOrder == request.SortOrder)
            .OrderByDescending(s => s.Id)
            .First();
        return new AddProductSpecificationResponse(addedSpec.Id);
    }
}
