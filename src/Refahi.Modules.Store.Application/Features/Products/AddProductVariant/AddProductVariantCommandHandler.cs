using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.Store.Application.Features.Products.AddProductVariant;

public class AddProductVariantCommandHandler : IRequestHandler<AddProductVariantCommand, AddProductVariantResponse>
{
    private readonly IProductRepository _productRepo;
    private readonly IMediator _mediator;

    public AddProductVariantCommandHandler(IProductRepository productRepo, IMediator mediator)
    {
        _productRepo = productRepo;
        _mediator = mediator;
    }

    public async Task<AddProductVariantResponse> Handle(AddProductVariantCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        var ap = await _mediator.Send(new GetAgreementProductByIdQuery(product.AgreementProductId), cancellationToken)
            ?? throw new StoreDomainException("اطلاعات محصول یافت نشد", "AGREEMENT_PRODUCT_NOT_FOUND");

        var salesModel = (SalesModel)ap.SalesModel;

        var combinations = request.Combinations
            .Select(c => (c.AttributeId, c.ValueId))
            .ToList();

        var addedVariant = product.AddVariant(
            combinations,
            request.StockCount,
            request.PriceMinor,
            request.DiscountedPriceMinor,
            request.ImageUrl,
            request.Sku,
            request.FromDate,
            request.ToDate,
            request.CapacityType,
            request.Capacity,
            salesModel);

        await _productRepo.AddProductVariantAsync(product, addedVariant, cancellationToken);

        return new AddProductVariantResponse(addedVariant.Id);
    }
}

