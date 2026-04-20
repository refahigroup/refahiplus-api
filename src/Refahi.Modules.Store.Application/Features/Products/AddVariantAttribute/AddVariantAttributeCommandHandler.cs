using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.AddVariantAttribute;

public class AddVariantAttributeCommandHandler : IRequestHandler<AddVariantAttributeCommand, AddVariantAttributeResponse>
{
    private readonly IProductRepository _productRepo;

    public AddVariantAttributeCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<AddVariantAttributeResponse> Handle(
        AddVariantAttributeCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        product.AddVariantAttribute(request.Name, request.SortOrder);

        await _productRepo.UpdateAsync(product, cancellationToken);

        var added = product.VariantAttributes
            .OrderByDescending(a => a.SortOrder)
            .First(a => a.Name == request.Name);

        return new AddVariantAttributeResponse(added.Id, added.Name);
    }
}
