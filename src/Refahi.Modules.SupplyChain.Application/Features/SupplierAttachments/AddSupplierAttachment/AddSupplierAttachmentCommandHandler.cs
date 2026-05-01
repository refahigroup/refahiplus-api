using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierAttachments;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.SupplierAttachments.AddSupplierAttachment;

public class AddSupplierAttachmentCommandHandler : IRequestHandler<AddSupplierAttachmentCommand, AddSupplierAttachmentResponse>
{
    private readonly ISupplierRepository _repository;

    public AddSupplierAttachmentCommandHandler(ISupplierRepository repository)
        => _repository = repository;

    public async Task<AddSupplierAttachmentResponse> Handle(AddSupplierAttachmentCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _repository.GetByIdAsync(request.SupplierId, true, cancellationToken)
            ?? throw new SupplyChainDomainException("تامین‌کننده یافت نشد", "SUPPLIER_NOT_FOUND");

        var attachment = supplier.AddAttachment(
            request.Title,
            request.FileUrl,
            request.FileName,
            request.ContentType,
            request.SizeBytes);

        _repository.Update(supplier);
        await _repository.SaveChangesAsync(cancellationToken);

        return new AddSupplierAttachmentResponse(attachment.Id);
    }
}
