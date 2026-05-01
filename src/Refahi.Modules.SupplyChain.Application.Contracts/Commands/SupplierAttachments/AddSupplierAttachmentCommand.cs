using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierAttachments;

public sealed record AddSupplierAttachmentCommand(
    Guid SupplierId,
    string Title,
    string FileUrl,
    string? FileName,
    string? ContentType,
    long? SizeBytes
) : IRequest<AddSupplierAttachmentResponse>;

public sealed record AddSupplierAttachmentResponse(Guid AttachmentId);
