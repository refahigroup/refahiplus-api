using Refahi.Modules.SupplyChain.Domain.Entities;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Domain.Exceptions;
using SupplyChainDeliveryType = Refahi.Modules.SupplyChain.Domain.Enums.DeliveryType;
using SupplyChainProductType = Refahi.Modules.SupplyChain.Domain.Enums.ProductType;
using SupplyChainSalesModel = Refahi.Modules.SupplyChain.Domain.Enums.SalesModel;

namespace Refahi.Modules.SupplyChain.Domain.Aggregates;

public sealed class Agreement
{
    private Agreement()
    {
        _products = new List<AgreementProduct>();
    }

    public Guid Id { get; private set; }
    public string AgreementNo { get; private set; } = string.Empty;
    public AgreementType AgreementType { get; private set; }
    public Guid SupplierId { get; private set; }

    // Navigation — intra-module (same bounded context), populated by Infrastructure for reads
    public Supplier? Supplier { get; private set; }
    public DateTimeOffset FromDate { get; private set; }
    public DateTimeOffset ToDate { get; private set; }
    public AgreementStatus Status { get; private set; }
    public string? StatusNote { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<AgreementProduct> _products;
    public IReadOnlyList<AgreementProduct> Products => _products.AsReadOnly();

    // --- Factory ---
    public static Agreement Create(
        string agreementNo,
        AgreementType agreementType,
        Guid supplierId,
        DateTimeOffset fromDate,
        DateTimeOffset toDate)
    {
        if (string.IsNullOrWhiteSpace(agreementNo))
            throw new SupplyChainDomainException("شماره قرارداد الزامی است", "AGREEMENT_NO_REQUIRED");

        if (toDate <= fromDate)
            throw new SupplyChainDomainException("تاریخ پایان باید بعد از تاریخ شروع باشد", "AGREEMENT_INVALID_DATE_RANGE");

        return new Agreement
        {
            Id = Guid.NewGuid(),
            AgreementNo = agreementNo.Trim(),
            AgreementType = agreementType,
            SupplierId = supplierId,
            FromDate = fromDate.ToUniversalTime(),
            ToDate = toDate.ToUniversalTime(),
            Status = AgreementStatus.Registered,
            StatusNote = null,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    // --- Behaviors ---

    public void UpdateDetails(
        string agreementNo,
        AgreementType agreementType,
        DateTimeOffset fromDate,
        DateTimeOffset toDate)
    {
        if (Status != AgreementStatus.Registered && Status != AgreementStatus.Rejected)
            throw new SupplyChainDomainException("ویرایش قرارداد تنها در وضعیت ثبت‌شده یا رد‌شده مجاز است", "STATUS_IMMUTABLE");

        if (toDate <= fromDate)
            throw new SupplyChainDomainException("تاریخ پایان باید بعد از تاریخ شروع باشد", "AGREEMENT_INVALID_DATE_RANGE");

        AgreementNo = agreementNo.Trim();
        AgreementType = agreementType;
        FromDate = fromDate.ToUniversalTime();
        ToDate = toDate.ToUniversalTime();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SubmitForReview()
    {
        if (Status != AgreementStatus.Registered)
            throw new SupplyChainDomainException("ارسال برای بررسی تنها از وضعیت ثبت‌شده مجاز است", "INVALID_STATUS_TRANSITION");

        Status = AgreementStatus.UnderReview;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Approve()
    {
        if (Status != AgreementStatus.UnderReview)
            throw new SupplyChainDomainException("تایید تنها از وضعیت در حال بررسی مجاز است", "INVALID_STATUS_TRANSITION");

        Status = AgreementStatus.Approved;
        StatusNote = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reject(string note)
    {
        if (Status != AgreementStatus.UnderReview)
            throw new SupplyChainDomainException("رد کردن تنها از وضعیت در حال بررسی مجاز است", "INVALID_STATUS_TRANSITION");

        Status = AgreementStatus.Rejected;
        StatusNote = note;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ResetToRegistered()
    {
        if (Status != AgreementStatus.UnderReview && Status != AgreementStatus.Rejected)
            throw new SupplyChainDomainException("بازگشت به وضعیت ثبت‌شده تنها از وضعیت در حال بررسی یا رد‌شده مجاز است", "INVALID_STATUS_TRANSITION");

        Status = AgreementStatus.Registered;
        StatusNote = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public AgreementProduct AddProduct(
        string name,
        string? description,
        int? categoryId,
        SupplyChainProductType productType,
        SupplyChainDeliveryType deliveryType,
        SupplyChainSalesModel salesModel,
        decimal commissionPercent)
    {
        if (Status != AgreementStatus.Registered && Status != AgreementStatus.Rejected)
            throw new SupplyChainDomainException("افزودن محصول تنها در وضعیت ثبت‌شده یا رد‌شده مجاز است", "STATUS_IMMUTABLE");

        var product = AgreementProduct.Create(Id, name, description, categoryId, productType, deliveryType, salesModel, commissionPercent);
        _products.Add(product);
        UpdatedAt = DateTimeOffset.UtcNow;
        return product;
    }

    public void UpdateProduct(
        Guid productId,
        string name,
        string? description,
        int? categoryId,
        SupplyChainProductType productType,
        SupplyChainDeliveryType deliveryType,
        SupplyChainSalesModel salesModel,
        decimal commissionPercent)
    {
        if (Status != AgreementStatus.Registered && Status != AgreementStatus.Rejected)
            throw new SupplyChainDomainException("ویرایش محصول تنها در وضعیت ثبت‌شده یا رد‌شده مجاز است", "STATUS_IMMUTABLE");

        var product = _products.FirstOrDefault(p => p.Id == productId && !p.IsDeleted)
            ?? throw new SupplyChainDomainException("محصول یافت نشد", "AGREEMENT_PRODUCT_NOT_FOUND");

        product.Update(name, description, categoryId, productType, deliveryType, salesModel, commissionPercent);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveProduct(Guid productId)
    {
        if (Status != AgreementStatus.Registered && Status != AgreementStatus.Rejected)
            throw new SupplyChainDomainException("حذف محصول تنها در وضعیت ثبت‌شده یا رد‌شده مجاز است", "STATUS_IMMUTABLE");

        var product = _products.FirstOrDefault(p => p.Id == productId && !p.IsDeleted)
            ?? throw new SupplyChainDomainException("محصول یافت نشد", "AGREEMENT_PRODUCT_NOT_FOUND");

        product.MarkDeleted();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkDeleted()
    {
        if (IsDeleted)
            throw new SupplyChainDomainException("قرارداد قبلاً حذف شده است", "AGREEMENT_ALREADY_DELETED");

        IsDeleted = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
