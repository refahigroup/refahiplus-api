using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierAttachments;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.SupplierAttachments.RemoveSupplierAttachment;

public class RemoveSupplierAttachmentCommandHandler : IRequestHandler<RemoveSupplierAttachmentCommand, Unit>
{
    private readonly ISupplierRepository _repository;

    public RemoveSupplierAttachmentCommandHandler(ISupplierRepository repository)
        => _repository = repository;

    public async Task<Unit> Handle(RemoveSupplierAttachmentCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _repository.GetByIdAsync(request.SupplierId, true, cancellationToken)
            ?? throw new SupplyChainDomainException("تامین‌کننده یافت نشد", "SUPPLIER_NOT_FOUND");

        supplier.RemoveAttachment(request.AttachmentId);

        _repository.Update(supplier);
        await _repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
