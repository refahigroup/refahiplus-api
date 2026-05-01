using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementProducts;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.AgreementProducts.AddAgreementProduct;

public class AddAgreementProductCommandHandler : IRequestHandler<AddAgreementProductCommand, AddAgreementProductResponse>
{
    private readonly IAgreementRepository _repository;

    public AddAgreementProductCommandHandler(IAgreementRepository repository)
        => _repository = repository;

    public async Task<AddAgreementProductResponse> Handle(AddAgreementProductCommand request, CancellationToken cancellationToken)
    {
        var agreement = await _repository.GetByIdAsync(request.AgreementId, true, cancellationToken)
            ?? throw new SupplyChainDomainException("قرارداد یافت نشد", "AGREEMENT_NOT_FOUND");

        var product = agreement.AddProduct(
            request.Name,
            request.Description,
            request.CategoryId,
            (ProductType)request.ProductType,
            (DeliveryType)request.DeliveryType,
            (SalesModel)request.SalesModel,
            request.CommissionPercent);

        // Explicitly register the new entity as Added — EF cannot auto-detect
        // new items added to a List<T> backing field as Added vs Modified.
        _repository.AddProduct(product);
        await _repository.SaveChangesAsync(cancellationToken);

        return new AddAgreementProductResponse(product.Id);
    }
}
