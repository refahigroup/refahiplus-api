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
    public PricingMode PricingMode { get; private set; }
    public decimal CommissionPercent { get; private set; }
    public bool VatApplicable { get; private set; }
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
        decimal commissionPercent,
        bool vatApplicable)
        => new()
        {
            Id = Guid.NewGuid(),
            AgreementId = agreementId,
            Name = name.Trim(),
            Description = description?.Trim(),
            CategoryId = categoryId,
            ProductType = productType,
            DeliveryType = deliveryType,
            SalesModel = NormalizeSalesModel(deliveryType, salesModel),
            PricingMode = ResolvePricingMode(deliveryType),
            CommissionPercent = commissionPercent,
            VatApplicable = vatApplicable,
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
        decimal commissionPercent,
        bool vatApplicable)
    {
        Name = name.Trim();
        Description = description?.Trim();
        CategoryId = categoryId;
        ProductType = productType;
        DeliveryType = deliveryType;
        SalesModel = NormalizeSalesModel(deliveryType, salesModel);
        PricingMode = ResolvePricingMode(deliveryType);
        CommissionPercent = commissionPercent;
        VatApplicable = vatApplicable;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    internal void MarkDeleted()
    {
        IsDeleted = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static SalesModel NormalizeSalesModel(DeliveryType deliveryType, SalesModel salesModel)
    {
        if (deliveryType == DeliveryType.InPerson)
            return SalesModel.Unlimited;
        if (salesModel == SalesModel.Unlimited)
            throw new InvalidOperationException("مدل فروش نامحدود فقط برای تحویل حضوری مجاز است");
        return salesModel;
    }

    private static PricingMode ResolvePricingMode(DeliveryType deliveryType)
        => deliveryType == DeliveryType.InPerson ? PricingMode.Manual : PricingMode.Fixed;
}
