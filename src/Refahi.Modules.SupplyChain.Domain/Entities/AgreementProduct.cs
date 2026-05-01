using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Domain.Entities;

public sealed class AgreementProduct
{
    private AgreementProduct() { }

    public Guid Id { get; private set; }
    public Guid AgreementId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int? CategoryId { get; private set; }
    public ProductType ProductType { get; private set; }
    public DeliveryType DeliveryType { get; private set; }
    public SalesModel SalesModel { get; private set; }
    public decimal CommissionPercent { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal static AgreementProduct Create(
        Guid agreementId,
        string name,
        string? description,
        int? categoryId,
        ProductType productType,
        DeliveryType deliveryType,
        SalesModel salesModel,
        decimal commissionPercent)
        => new()
        {
            Id = Guid.NewGuid(),
            AgreementId = agreementId,
            Name = name.Trim(),
            Description = description?.Trim(),
            CategoryId = categoryId,
            ProductType = productType,
            DeliveryType = deliveryType,
            SalesModel = salesModel,
            CommissionPercent = commissionPercent,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    internal void Update(
        string name,
        string? description,
        int? categoryId,
        ProductType productType,
        DeliveryType deliveryType,
        SalesModel salesModel,
        decimal commissionPercent)
    {
        Name = name.Trim();
        Description = description?.Trim();
        CategoryId = categoryId;
        ProductType = productType;
        DeliveryType = deliveryType;
        SalesModel = salesModel;
        CommissionPercent = commissionPercent;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    internal void MarkDeleted()
    {
        IsDeleted = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
