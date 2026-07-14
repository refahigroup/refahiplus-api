using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Products.UpdateVariantAttribute;

public sealed class UpdateVariantAttributeCommandHandler : IRequestHandler<UpdateVariantAttributeCommand, Unit>
{
    private readonly IProductRepository _productRepo;

    public UpdateVariantAttributeCommandHandler(IProductRepository productRepo)
        => _productRepo = productRepo;

    public async Task<Unit> Handle(UpdateVariantAttributeCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        product.UpdateVariantAttribute(request.AttributeId, request.Name, request.SortOrder);
        await _productRepo.UpdateAsync(product, cancellationToken);

        return Unit.Value;
    }
}
