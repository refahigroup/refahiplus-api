using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.Store.Application.Features.Products.UpdateProductVariant;

public sealed class UpdateProductVariantCommandHandler : IRequestHandler<UpdateProductVariantCommand, Unit>
{
    private readonly IProductRepository _productRepo;
    private readonly IMediator _mediator;

    public UpdateProductVariantCommandHandler(IProductRepository productRepo, IMediator mediator)
    {
        _productRepo = productRepo;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(UpdateProductVariantCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");
        var agreementProduct = await _mediator.Send(
                new GetAgreementProductByIdQuery(product.AgreementProductId), cancellationToken)
            ?? throw new StoreDomainException("اطلاعات محصول یافت نشد", "AGREEMENT_PRODUCT_NOT_FOUND");

        product.UpdateVariant(
            request.VariantId,
            request.Combinations.Select(c => (c.AttributeId, c.ValueId)).ToList(),
            request.StockCount,
            request.PriceMinor,
            request.DiscountedPriceMinor,
            request.ImageUrl,
            request.Sku,
            request.FromDate,
            request.ToDate,
            request.CapacityType,
            request.Capacity,
            (SalesModel)agreementProduct.SalesModel);
        await _productRepo.UpdateAsync(product, cancellationToken);

        return Unit.Value;
    }
}
