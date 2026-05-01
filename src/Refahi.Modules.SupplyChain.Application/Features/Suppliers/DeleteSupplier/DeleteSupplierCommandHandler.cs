using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.Suppliers.DeleteSupplier;

public class DeleteSupplierCommandHandler : IRequestHandler<DeleteSupplierCommand, Unit>
{
    private readonly ISupplierRepository _repository;

    public DeleteSupplierCommandHandler(ISupplierRepository repository)
        => _repository = repository;

    public async Task<Unit> Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _repository.GetByIdAsync(request.Id, false, cancellationToken)
            ?? throw new SupplyChainDomainException("تامین‌کننده یافت نشد", "SUPPLIER_NOT_FOUND");

        supplier.MarkDeleted();

        _repository.Update(supplier);
        await _repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
