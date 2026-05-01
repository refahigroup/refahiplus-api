using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Agreements;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.Agreements.UpdateAgreement;

public class UpdateAgreementCommandHandler : IRequestHandler<UpdateAgreementCommand, Unit>
{
    private readonly IAgreementRepository _repository;

    public UpdateAgreementCommandHandler(IAgreementRepository repository)
        => _repository = repository;

    public async Task<Unit> Handle(UpdateAgreementCommand request, CancellationToken cancellationToken)
    {
        var agreement = await _repository.GetByIdAsync(request.Id, false, cancellationToken)
            ?? throw new SupplyChainDomainException("قرارداد یافت نشد", "AGREEMENT_NOT_FOUND");

        var exists = await _repository.ExistsByAgreementNoAsync(request.AgreementNo, request.Id, cancellationToken);
        if (exists)
            throw new SupplyChainDomainException("شماره قرارداد تکراری است", "AGREEMENT_NO_DUPLICATED");

        agreement.UpdateDetails(
            request.AgreementNo,
            (AgreementType)request.Type,
            request.FromDate,
            request.ToDate);

        _repository.Update(agreement);
        await _repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
