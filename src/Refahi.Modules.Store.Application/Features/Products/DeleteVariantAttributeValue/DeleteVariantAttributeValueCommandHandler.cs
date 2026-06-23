using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.DeleteVariantAttributeValue;

public class DeleteVariantAttributeValueCommandHandler : IRequestHandler<DeleteVariantAttributeValueCommand, Unit>
{
    private readonly IProductRepository _productRepo;

    public DeleteVariantAttributeValueCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<Unit> Handle(DeleteVariantAttributeValueCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        product.RemoveVariantAttributeValue(request.AttributeId, request.ValueId);

        await _productRepo.UpdateAsync(product, cancellationToken);
        return Unit.Value;
    }
}