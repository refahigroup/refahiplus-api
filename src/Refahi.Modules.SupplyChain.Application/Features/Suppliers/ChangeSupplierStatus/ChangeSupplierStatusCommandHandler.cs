using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Domain.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreateWallet;

namespace Refahi.Modules.SupplyChain.Application.Features.Suppliers.ChangeSupplierStatus;

public class ChangeSupplierStatusCommandHandler : IRequestHandler<ChangeSupplierStatusCommand, Unit>
{
    private readonly ISupplierRepository _repository;
    private readonly IMediator _mediator;

    public ChangeSupplierStatusCommandHandler(ISupplierRepository repository, IMediator mediator)
    {
        _repository = repository;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(
        ChangeSupplierStatusCommand request,
        CancellationToken cancellationToken
    )
    {
        var supplier =
            await _repository.GetByIdAsync(request.Id, false, cancellationToken)
            ?? throw new SupplyChainDomainException("تامین‌کننده یافت نشد", "SUPPLIER_NOT_FOUND");

        var newStatus = (SupplierStatus)request.NewStatus;

        switch (newStatus)
        {
            case SupplierStatus.UnderReview:
                supplier.SubmitForReview();
                break;
            case SupplierStatus.Approved:
                supplier.Approve();
                await _mediator.Send(
                    new CreateWalletCommand(supplier.Id, WalletTypeCodes.Provider, "IRR"),
                    cancellationToken
                );
                break;
            case SupplierStatus.Rejected:
                supplier.Reject(request.Note ?? string.Empty);
                break;
            case SupplierStatus.Registered:
                supplier.ResetToRegistered();
                break;
            default:
                throw new SupplyChainDomainException(
                    "وضعیت درخواستی معتبر نیست",
                    "INVALID_STATUS_TRANSITION"
                );
        }

        _repository.Update(supplier);
        await _repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
