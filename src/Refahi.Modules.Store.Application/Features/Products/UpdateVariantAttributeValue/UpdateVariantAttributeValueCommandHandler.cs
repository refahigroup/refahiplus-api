using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.UpdateVariantAttributeValue;

public sealed class UpdateVariantAttributeValueCommandHandler : IRequestHandler<UpdateVariantAttributeValueCommand, Unit>
{
    private readonly IProductRepository _productRepo;

    public UpdateVariantAttributeValueCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<Unit> Handle(UpdateVariantAttributeValueCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        product.UpdateVariantAttributeValue(
            request.AttributeId,
            request.ValueId,
            request.Value,
            request.SortOrder);
        await _productRepo.UpdateAsync(product, cancellationToken);

        return Unit.Value;
    }
}
