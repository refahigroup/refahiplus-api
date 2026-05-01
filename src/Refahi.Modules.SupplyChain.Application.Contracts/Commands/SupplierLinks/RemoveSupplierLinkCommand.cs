using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierLinks;

public sealed record RemoveSupplierLinkCommand(
    Guid SupplierId,
    Guid LinkId
) : IRequest<Unit>;
