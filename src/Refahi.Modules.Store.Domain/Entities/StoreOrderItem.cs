using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class StoreOrderItem
{
    private StoreOrderItem() { }

    public Guid Id { get; private set; }
    public Guid StoreOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid SourceCartItemId { get; private set; }
    public Guid? ProductVariantId { get; private set; }
    public Guid? ProductSessionId { get; private set; }
    public Guid? OfferId { get; private set; }
    public string ProductTitle { get; private set; } = string.Empty;
    public string? VariantTitle { get; private set; }
    public string? SessionTitle { get; private set; }
    public int CategoryId { get; private set; }
    public string CategoryCode { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Guid ShopId { get; private set; }
    public SalesChannel SalesChannel { get; private set; }
    public ProductType ProductType { get; private set; }
    public SalesModel SalesModel { get; private set; }
    public FulfillmentMethod FulfillmentMethod { get; private set; }
    public int Quantity { get; private set; }
    public long OriginalUnitPriceMinor { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public long FinalUnitPriceMinor { get; private set; }
    public long UnitPriceMinor { get; private set; }
    public long GrossAmountMinor { get; private set; }
    public long? DeclaredGrossAmountMinor { get; private set; }
    public Guid AgreementId { get; private set; }
    public Guid AgreementCategoryTermId { get; private set; }
    public decimal CommissionPercent { get; private set; }
    public long CommissionAmountMinor { get; private set; }
    public DateOnly? UsageDate { get; private set; }
    public short DeliveryMethod { get; private set; }

    internal static StoreOrderItem Create(Guid storeOrderId, StoreOrderItemSnapshot snapshot)
    {
        if (
            snapshot.Quantity <= 0
            || snapshot.FinalUnitPriceMinor < 0
            || snapshot.OriginalUnitPriceMinor <= 0
        )
            throw new Exceptions.StoreDomainException(
                "اطلاعات مالی آیتم سفارش معتبر نیست",
                "INVALID_STORE_ORDER_ITEM"
            );

        if (
            snapshot.SalesChannel == SalesChannel.InPerson
            && (
                snapshot.Quantity != 1
                || !snapshot.DeclaredGrossAmountMinor.HasValue
                || snapshot.DeclaredGrossAmountMinor <= 0
                || snapshot.OfferId.HasValue
                || snapshot.OriginalUnitPriceMinor != snapshot.DeclaredGrossAmountMinor
                || snapshot.FinalUnitPriceMinor != snapshot.DeclaredGrossAmountMinor
            )
        )
            throw new Exceptions.StoreDomainException(
                "فروش حضوری باید یک آیتم با تعداد یک و مبلغ کل معتبر داشته باشد",
                "INVALID_IN_PERSON_ITEM"
            );

        var gross = checked(snapshot.FinalUnitPriceMinor * snapshot.Quantity);
        var commission = checked(
            (long)
                Math.Round(
                    gross * snapshot.CommissionPercent / 100m,
                    0,
                    MidpointRounding.AwayFromZero
                )
        );

        return new StoreOrderItem
        {
            Id = Guid.NewGuid(),
            StoreOrderId = storeOrderId,
            ProductId = snapshot.ProductId,
            ProductVariantId = snapshot.ProductVariantId,
            SourceCartItemId = snapshot.SourceCartItemId,
            ProductSessionId = snapshot.ProductSessionId,
            OfferId = snapshot.OfferId,
            ProductTitle = snapshot.ProductTitle,
            VariantTitle = snapshot.VariantTitle,
            SessionTitle = snapshot.SessionTitle,
            CategoryId = snapshot.CategoryId,
            CategoryCode = snapshot.CategoryCode,
            SupplierId = snapshot.SupplierId,
            ShopId = snapshot.ShopId,
            SalesChannel = snapshot.SalesChannel,
            ProductType = snapshot.ProductType,
            SalesModel = snapshot.SalesModel,
            FulfillmentMethod = snapshot.FulfillmentMethod,
            Quantity = snapshot.Quantity,
            OriginalUnitPriceMinor = snapshot.OriginalUnitPriceMinor,
            DiscountPercent = snapshot.DiscountPercent,
            FinalUnitPriceMinor = snapshot.FinalUnitPriceMinor,
            UnitPriceMinor = snapshot.FinalUnitPriceMinor,
            GrossAmountMinor = gross,
            DeclaredGrossAmountMinor = snapshot.DeclaredGrossAmountMinor,
            AgreementId = snapshot.AgreementId,
            AgreementCategoryTermId = snapshot.AgreementCategoryTermId,
            CommissionPercent = snapshot.CommissionPercent,
            CommissionAmountMinor = commission,
            UsageDate = snapshot.UsageDate,
            DeliveryMethod = snapshot.DeliveryMethod,
        };
    }
}

public sealed record StoreOrderItemSnapshot(
    Guid SourceCartItemId,
    Guid ProductId,
    Guid? ProductVariantId,
    Guid? ProductSessionId,
    Guid? OfferId,
    string ProductTitle,
    string? VariantTitle,
    string? SessionTitle,
    int CategoryId,
    string CategoryCode,
    Guid SupplierId,
    Guid ShopId,
    SalesChannel SalesChannel,
    ProductType ProductType,
    SalesModel SalesModel,
    FulfillmentMethod FulfillmentMethod,
    int Quantity,
    long OriginalUnitPriceMinor,
    decimal DiscountPercent,
    long FinalUnitPriceMinor,
    Guid AgreementId,
    Guid AgreementCategoryTermId,
    decimal CommissionPercent,
    DateOnly? UsageDate,
    short DeliveryMethod = 0,
    long? DeclaredGrossAmountMinor = null
);
