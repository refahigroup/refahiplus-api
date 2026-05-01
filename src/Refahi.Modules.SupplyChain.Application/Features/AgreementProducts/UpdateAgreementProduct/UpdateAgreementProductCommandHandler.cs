using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementProducts;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.AgreementProducts.UpdateAgreementProduct;

public class UpdateAgreementProductCommandHandler : IRequestHandler<UpdateAgreementProductCommand, Unit>
{
    private readonly IAgreementRepository _repository;

    public UpdateAgreementProductCommandHandler(IAgreementRepository repository)
        => _repository = repository;

    public async Task<Unit> Handle(UpdateAgreementProductCommand request, CancellationToken cancellationToken)
    {
        var agreement = await _repository.GetByIdAsync(request.AgreementId, true, cancellationToken)
            ?? throw new SupplyChainDomainException("قرارداد یافت نشد", "AGREEMENT_NOT_FOUND");

        agreement.UpdateProduct(
            request.ProductId,
            request.Name,
            request.Description,
            request.CategoryId,
            (ProductType)request.ProductType,
            (DeliveryType)request.DeliveryType,
            (SalesModel)request.SalesModel,
            request.CommissionPercent);

        await _repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
