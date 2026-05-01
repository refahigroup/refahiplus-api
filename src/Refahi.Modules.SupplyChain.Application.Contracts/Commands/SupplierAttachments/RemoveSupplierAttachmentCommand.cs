using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierAttachments;

public sealed record RemoveSupplierAttachmentCommand(
    Guid SupplierId,
    Guid AttachmentId
) : IRequest<Unit>;
