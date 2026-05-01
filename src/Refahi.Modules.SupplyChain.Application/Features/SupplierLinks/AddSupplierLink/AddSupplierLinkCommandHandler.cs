using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierLinks;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Application.Features.SupplierLinks.AddSupplierLink;

public class AddSupplierLinkCommandHandler : IRequestHandler<AddSupplierLinkCommand, AddSupplierLinkResponse>
{
    private readonly ISupplierRepository _repository;

    public AddSupplierLinkCommandHandler(ISupplierRepository repository)
        => _repository = repository;

    public async Task<AddSupplierLinkResponse> Handle(AddSupplierLinkCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _repository.GetByIdAsync(request.SupplierId, true, cancellationToken)
            ?? throw new SupplyChainDomainException("تامین‌کننده یافت نشد", "SUPPLIER_NOT_FOUND");

        var link = supplier.AddLink((SupplierLinkType)request.Type, request.Url, request.Label);

        _repository.Update(supplier);
        await _repository.SaveChangesAsync(cancellationToken);

        return new AddSupplierLinkResponse(link.Id);
    }
}
