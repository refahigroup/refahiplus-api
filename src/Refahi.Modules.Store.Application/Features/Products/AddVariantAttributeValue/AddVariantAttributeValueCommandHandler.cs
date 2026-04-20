using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.AddVariantAttributeValue;

public class AddVariantAttributeValueCommandHandler : IRequestHandler<AddVariantAttributeValueCommand, AddVariantAttributeValueResponse>
{
    private readonly IProductRepository _productRepo;

    public AddVariantAttributeValueCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<AddVariantAttributeValueResponse> Handle(
        AddVariantAttributeValueCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        product.AddVariantAttributeValue(request.AttributeId, request.Value, request.SortOrder);

        await _productRepo.UpdateAsync(product, cancellationToken);

        var attr = product.VariantAttributes.First(a => a.Id == request.AttributeId);
        var added = attr.Values.Last(v => v.Value == request.Value);

        return new AddVariantAttributeValueResponse(added.Id, added.Value);
    }
}
