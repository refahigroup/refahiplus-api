using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierLinks;

public sealed record AddSupplierLinkCommand(
    Guid SupplierId,
    short Type,
    string Url,
    string? Label
) : IRequest<AddSupplierLinkResponse>;

public sealed record AddSupplierLinkResponse(Guid LinkId);
