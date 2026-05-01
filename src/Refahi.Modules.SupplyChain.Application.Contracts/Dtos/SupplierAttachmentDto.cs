namespace Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

public sealed record SupplierAttachmentDto(
    Guid Id,
    string Title,
    string FileUrl,
    string? FileName,
    string? ContentType,
    long? SizeBytes,
    DateTimeOffset CreatedAt);
