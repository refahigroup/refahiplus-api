using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Agreements;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.Agreements.ChangeAgreementStatus;

public class ChangeAgreementStatusCommandHandler : IRequestHandler<ChangeAgreementStatusCommand, Unit>
{
    private readonly IAgreementRepository _repository;

    public ChangeAgreementStatusCommandHandler(IAgreementRepository repository)
        => _repository = repository;

    public async Task<Unit> Handle(ChangeAgreementStatusCommand request, CancellationToken cancellationToken)
    {
        var agreement = await _repository.GetByIdAsync(request.Id, false, cancellationToken)
            ?? throw new SupplyChainDomainException("قرارداد یافت نشد", "AGREEMENT_NOT_FOUND");

        var newStatus = (AgreementStatus)request.NewStatus;

        switch (newStatus)
        {
            case AgreementStatus.UnderReview:
                agreement.SubmitForReview();
                break;
            case AgreementStatus.Approved:
                agreement.Approve();
                break;
            case AgreementStatus.Rejected:
                agreement.Reject(request.Note ?? string.Empty);
                break;
            case AgreementStatus.Registered:
                agreement.ResetToRegistered();
                break;
            default:
                throw new SupplyChainDomainException("وضعیت درخواستی معتبر نیست", "INVALID_STATUS_TRANSITION");
        }

        _repository.Update(agreement);
        await _repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
