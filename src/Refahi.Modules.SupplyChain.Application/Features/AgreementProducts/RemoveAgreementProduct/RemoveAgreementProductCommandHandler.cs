using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementProducts;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.AgreementProducts.RemoveAgreementProduct;

public class RemoveAgreementProductCommandHandler : IRequestHandler<RemoveAgreementProductCommand, Unit>
{
    private readonly IAgreementRepository _repository;

    public RemoveAgreementProductCommandHandler(IAgreementRepository repository)
        => _repository = repository;

    public async Task<Unit> Handle(RemoveAgreementProductCommand request, CancellationToken cancellationToken)
    {
        var agreement = await _repository.GetByIdAsync(request.AgreementId, true, cancellationToken)
            ?? throw new SupplyChainDomainException("قرارداد یافت نشد", "AGREEMENT_NOT_FOUND");

        agreement.RemoveProduct(request.ProductId);

        await _repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
