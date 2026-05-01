namespace Refahi.Modules.SupplyChain.Domain.Entities;

public sealed class SupplierAttachment
{
    private SupplierAttachment() { }

    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string FileUrl { get; private set; } = string.Empty;
    public string? FileName { get; private set; }
    public string? ContentType { get; private set; }
    public long? SizeBytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static SupplierAttachment Create(
        Guid supplierId, string title, string fileUrl,
        string? fileName, string? contentType, long? sizeBytes)
        => new()
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            Title = title,
            FileUrl = fileUrl,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            CreatedAt = DateTimeOffset.UtcNow
        };
}
