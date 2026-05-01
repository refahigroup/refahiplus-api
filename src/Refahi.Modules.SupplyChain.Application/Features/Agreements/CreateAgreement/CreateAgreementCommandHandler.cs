using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Agreements;
using Refahi.Modules.SupplyChain.Domain.Aggregates;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.Agreements.CreateAgreement;

public class CreateAgreementCommandHandler : IRequestHandler<CreateAgreementCommand, CreateAgreementResponse>
{
    private readonly IAgreementRepository _repository;
    private readonly ISupplierRepository _supplierRepository;

    public CreateAgreementCommandHandler(IAgreementRepository repository, ISupplierRepository supplierRepository)
    {
        _repository = repository;
        _supplierRepository = supplierRepository;
    }

    public async Task<CreateAgreementResponse> Handle(CreateAgreementCommand request, CancellationToken cancellationToken)
    {
        var exists = await _repository.ExistsByAgreementNoAsync(request.AgreementNo, null, cancellationToken);
        if (exists)
            throw new SupplyChainDomainException("شماره قرارداد تکراری است", "AGREEMENT_NO_DUPLICATED");

        var supplierExists = await _supplierRepository.GetByIdAsync(request.SupplierId, false, cancellationToken);
        if (supplierExists is null)
            throw new SupplyChainDomainException("تامین‌کننده یافت نشد", "SUPPLIER_NOT_FOUND");

        var agreement = Agreement.Create(
            request.AgreementNo,
            (AgreementType)request.Type,
            request.SupplierId,
            request.FromDate,
            request.ToDate);

        await _repository.AddAsync(agreement, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return new CreateAgreementResponse(agreement.Id);
    }
}
