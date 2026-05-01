using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Agreements;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.Agreements.DeleteAgreement;

public class DeleteAgreementCommandHandler : IRequestHandler<DeleteAgreementCommand, Unit>
{
    private readonly IAgreementRepository _repository;

    public DeleteAgreementCommandHandler(IAgreementRepository repository)
        => _repository = repository;

    public async Task<Unit> Handle(DeleteAgreementCommand request, CancellationToken cancellationToken)
    {
        var agreement = await _repository.GetByIdAsync(request.Id, false, cancellationToken)
            ?? throw new SupplyChainDomainException("قرارداد یافت نشد", "AGREEMENT_NOT_FOUND");

        agreement.MarkDeleted();

        _repository.Update(agreement);
        await _repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
