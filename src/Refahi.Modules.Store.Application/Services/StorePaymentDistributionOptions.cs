namespace Refahi.Modules.Store.Application.Services;

public sealed class StorePaymentDistributionOptions
{
    public const string SectionName = "StorePaymentDistribution";
    public Guid RefahiRevenueWalletId { get; init; }
    public Guid RefahiVatWalletId { get; init; }
    public decimal VatRatePercent { get; init; }
}

public sealed record StoreInPersonFinancialPlan(
    long GrossAmountMinor,
    decimal CommissionPercent,
    long CommissionAmountMinor,
    decimal VatPercent,
    long VatAmountMinor,
    long VendorNetAmountMinor,
    Guid VendorWalletId,
    IReadOnlyList<Refahi.Modules.Orders.Application.Contracts.Commands.OrderPaymentPostingInput> Postings);

public interface IStoreInPersonFinancialPlanner
{
    Task<StoreInPersonFinancialPlan> BuildAsync(
        Guid supplierId, long grossAmountMinor, decimal commissionPercent,
        bool vatApplicable, CancellationToken ct);
}
