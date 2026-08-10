namespace Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

[Obsolete(
    "AgreementProduct یک قرارداد سازگاری legacy است؛ برای توسعه جدید از AgreementCategoryTerm استفاده کنید."
)]
public sealed record AgreementProductDto(
    Guid Id,
    Guid AgreementId,
    string Name,
    string? Description,
    int? CategoryId,
    string? CategoryName,
    short ProductType,
    short DeliveryType,
    short SalesModel,
    decimal CommissionPercent,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    short PricingMode = 1,
    bool VatApplicable = false,
    Guid? SupplierId = null
);
