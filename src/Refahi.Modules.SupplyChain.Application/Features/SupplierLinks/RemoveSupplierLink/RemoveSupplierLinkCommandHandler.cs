using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierLinks;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.SupplierLinks.RemoveSupplierLink;

public class RemoveSupplierLinkCommandHandler : IRequestHandler<RemoveSupplierLinkCommand, Unit>
{
    private readonly ISupplierRepository _repository;

    public RemoveSupplierLinkCommandHandler(ISupplierRepository repository)
        => _repository = repository;

    public async Task<Unit> Handle(RemoveSupplierLinkCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _repository.GetByIdAsync(request.SupplierId, true, cancellationToken)
            ?? throw new SupplyChainDomainException("تامین‌کننده یافت نشد", "SUPPLIER_NOT_FOUND");

        supplier.RemoveLink(request.LinkId);

        _repository.Update(supplier);
        await _repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
