using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Domain.Exceptions;

namespace Refahi.Modules.SupplyChain.Domain.Entities;

public sealed class AgreementCategoryTerm
{
    private const SalesChannel AllChannels = SalesChannel.Online | SalesChannel.InPerson;

    private AgreementCategoryTerm() { }

    public Guid Id { get; private set; }
    public Guid AgreementId { get; private set; }
    public int CategoryId { get; private set; }
    public SalesChannel AllowedSalesChannels { get; private set; }
    public decimal CommissionPercent { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal static AgreementCategoryTerm Create(
        Guid agreementId,
        int categoryId,
        SalesChannel allowedSalesChannels,
        decimal commissionPercent)
    {
        Validate(categoryId, allowedSalesChannels, commissionPercent);
        var now = DateTimeOffset.UtcNow;

        return new AgreementCategoryTerm
        {
            Id = Guid.NewGuid(),
            AgreementId = agreementId,
            CategoryId = categoryId,
            AllowedSalesChannels = allowedSalesChannels,
            CommissionPercent = commissionPercent,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    internal void Update(
        int categoryId,
        SalesChannel allowedSalesChannels,
        decimal commissionPercent)
    {
        Validate(categoryId, allowedSalesChannels, commissionPercent);
        CategoryId = categoryId;
        AllowedSalesChannels = allowedSalesChannels;
        CommissionPercent = commissionPercent;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    internal void MarkDeleted()
    {
        if (IsDeleted)
            throw new SupplyChainDomainException("شرط دسته‌بندی قرارداد قبلاً حذف شده است", "AGREEMENT_CATEGORY_TERM_ALREADY_DELETED");

        IsDeleted = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void Validate(
        int categoryId,
        SalesChannel allowedSalesChannels,
        decimal commissionPercent)
    {
        if (categoryId <= 0)
            throw new SupplyChainDomainException("شناسه دسته‌بندی الزامی است", "CATEGORY_ID_REQUIRED");

        if (allowedSalesChannels == SalesChannel.None || (allowedSalesChannels & ~AllChannels) != 0)
            throw new SupplyChainDomainException("کانال فروش نامعتبر است", "SALES_CHANNEL_INVALID");

        if (commissionPercent is < 0 or > 100)
            throw new SupplyChainDomainException("درصد کمیسیون باید بین ۰ تا ۱۰۰ باشد", "COMMISSION_PERCENT_INVALID");

        if (decimal.Round(commissionPercent, 2) != commissionPercent)
            throw new SupplyChainDomainException("درصد کمیسیون حداکثر دو رقم اعشار دارد", "COMMISSION_PERCENT_SCALE_INVALID");
    }
}
