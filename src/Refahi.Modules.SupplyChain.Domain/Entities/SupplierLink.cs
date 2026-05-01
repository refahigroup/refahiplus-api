using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Domain.Entities;

public sealed class SupplierLink
{
    private SupplierLink() { }

    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public SupplierLinkType Type { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string? Label { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static SupplierLink Create(Guid supplierId, SupplierLinkType type, string url, string? label)
        => new()
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            Type = type,
            Url = url,
            Label = label,
            CreatedAt = DateTimeOffset.UtcNow
        };
}
