using Microsoft.Extensions.Configuration;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Domain.Enums;
using System.Text.Json;

namespace Refahi.Modules.Charge.Application.Services;

public sealed class ChargeRequestQuoteService
{
    private readonly IChargeProviderResolver _providers;
    private readonly ChargePricingService _pricing;
    private readonly IConfiguration _configuration;

    public ChargeRequestQuoteService(
        IChargeProviderResolver providers,
        ChargePricingService pricing,
        IConfiguration configuration)
    {
        _providers = providers;
        _pricing = pricing;
        _configuration = configuration;
    }

    public async Task<ResolvedChargeQuote> ResolveAsync(ChargeSelection selection, CancellationToken ct)
    {
        var capability = ChargeCapabilityPolicy.Get(selection.Operator, selection.ServiceType);

        if (!capability.IsSupported)
            throw new ArgumentException(capability.UnavailableReason);

        if (selection.RequestedAmountMinor.HasValue &&
            (selection.RequestedAmountMinor < capability.MinimumAmountMinor || selection.RequestedAmountMinor > capability.MaximumAmountMinor))
        {
            throw new ArgumentException("مبلغ انتخاب‌شده خارج از محدوده مجاز این خدمت است");
        }

        var provider = _providers.GetDefault();
        var product = await ResolveSelectionAsync(provider, selection, ct);
        var now = DateTime.UtcNow;

        var price = await _pricing.CalculateAsync(
            selection.Operator,
            selection.ServiceType,
            product.CostMinor,
            now,
            ct);

        var ttlMinutes = int.TryParse(
            _configuration["Charge:RequestTtlMinutes"],
            out var configuredTtl)
            ? Math.Clamp(configuredTtl, 1, 120)
            : 20;

        return new ResolvedChargeQuote(
            provider.Name,
            product.ProductId,
            product.Caption,
            product.ProductCategory,
            product.PayBill,
            product.SnapshotJson,
            product.CostMinor,
            price.RuleId,
            price.Percent,
            price.FixedAmountMinor,
            price.MarkupAmountMinor,
            price.FinalAmountMinor,
            now.AddMinutes(ttlMinutes));
    }

    private static Task<ResolvedSelection> ResolveSelectionAsync(
        IChargeProvider provider,
        ChargeSelection selection,
        CancellationToken ct)
        => selection.ServiceType switch
        {
            ChargeServiceType.DirectCharge => ResolveDirectAsync(provider, selection, ct),
            ChargeServiceType.InternetPackage => ResolvePackageAsync(provider, selection, ct),
            ChargeServiceType.PostpaidBill => ResolveBillAsync(provider, selection, ct),
            ChargeServiceType.CreditLimit => Task.FromResult(ResolveCredit(selection)),
            ChargeServiceType.PinCharge => ResolvePinAsync(provider, selection, ct),
            _ => throw new ArgumentException("نوع خدمت پشتیبانی نمی‌شود")
        };

    private static async Task<ResolvedSelection> ResolveDirectAsync(
        IChargeProvider provider,
        ChargeSelection selection,
        CancellationToken ct)
    {
        var amount = selection.RequestedAmountMinor ?? 0;
        var minimum = ChargeCapabilityPolicy
            .Get(selection.Operator, selection.ServiceType)
            .MinimumAmountMinor ?? 1;

        if (amount < minimum)
            throw new ArgumentException($"حداقل مبلغ شارژ این اپراتور {minimum} ریال است");

        if (selection.Operator is ChargeOperator.Irancell or ChargeOperator.Mci)
        {
            var eligibility = await provider.CheckEligibilityAsync(
                new(selection.Operator, selection.DestinationMobileNumber, amount, "CUSTOM", 1001),
                ct);
            if (eligibility.Supported && eligibility.Eligible == false)
                throw new ArgumentException(eligibility.Message ?? "امکان خرید این شارژ وجود ندارد");
        }

        return new("CUSTOM", "شارژ مستقیم", 1001, 0, amount, JsonSerializer.Serialize(new { amount }));
    }

    private static async Task<ResolvedSelection> ResolvePackageAsync(
        IChargeProvider provider,
        ChargeSelection selection,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(selection.ProviderProductId))
            throw new ArgumentException("شناسه بسته الزامی است");

        var product = (await provider.GetProductsAsync(selection.Operator, ct))
            .FirstOrDefault(x => x.ProviderProductId == selection.ProviderProductId);

        product ??= (await provider.GetOffersAsync(
                selection.Operator,
                selection.DestinationMobileNumber,
                ChargeOfferCategory.All,
                ct))
            .FirstOrDefault(x => x.ProviderProductId == selection.ProviderProductId);

        if (product is null || !product.IsActive)
            throw new ArgumentException("بسته انتخاب‌شده معتبر یا فعال نیست");

        var amount = product.AmountWithTaxMinor > 0
            ? product.AmountWithTaxMinor
            : product.AmountMinor;

        if (selection.Operator is ChargeOperator.Irancell or ChargeOperator.Mci)
        {
            var eligibility = await provider.CheckEligibilityAsync(
                new(selection.Operator, selection.DestinationMobileNumber, amount, product.ProviderProductId, 1002),
                ct);
            if (eligibility.Supported && eligibility.Eligible == false)
                throw new ArgumentException(eligibility.Message ?? "امکان خرید این بسته وجود ندارد");
        }

        return new(
            product.ProviderProductId,
            product.Title,
            1002,
            0,
            amount,
            JsonSerializer.Serialize(product));
    }

    private static async Task<ResolvedSelection> ResolveBillAsync(
        IChargeProvider provider,
        ChargeSelection selection,
        CancellationToken ct)
    {
        if (selection.Operator != ChargeOperator.Irancell)
            throw new ArgumentException("پرداخت قبض در این قرارداد فقط برای ایرانسل پشتیبانی می‌شود");

        var balance = await provider.GetPostpaidBalanceAsync(
            selection.Operator,
            selection.DestinationMobileNumber,
            ct);
        var amount = balance.OutstandingBalanceMinor ?? 0;
        if (amount <= 0)
            throw new ArgumentException("بدهی قابل پرداختی برای این شماره وجود ندارد");

        return new("CUSTOM", "پرداخت قبض ایرانسل", 1001, 1, amount, JsonSerializer.Serialize(balance));
    }

    private static ResolvedSelection ResolveCredit(ChargeSelection selection)
    {
        if (selection.Operator != ChargeOperator.Irancell)
            throw new ArgumentException("افزایش حد اعتبار فقط برای ایرانسل پشتیبانی می‌شود");
        if ((selection.RequestedAmountMinor ?? 0) <= 0)
            throw new ArgumentException("مبلغ افزایش اعتبار معتبر نیست");

        return new(
            "CUSTOM",
            "افزایش حد اعتبار ایرانسل",
            1001,
            2,
            selection.RequestedAmountMinor!.Value,
            JsonSerializer.Serialize(new { amount = selection.RequestedAmountMinor }));
    }

    private static async Task<ResolvedSelection> ResolvePinAsync(
        IChargeProvider provider,
        ChargeSelection selection,
        CancellationToken ct)
    {
        if (selection.Operator is not (ChargeOperator.Irancell or ChargeOperator.Mci or ChargeOperator.Rightel))
            throw new ArgumentException("این اپراتور پین شارژ ارائه نمی‌دهد");
        if (!selection.PinCategoryId.HasValue)
            throw new ArgumentException("دسته‌بندی پین الزامی است");

        var category = (await provider.GetPinCategoriesAsync(ct))
            .FirstOrDefault(x => x.CategoryId == selection.PinCategoryId);
        if (category is null)
            throw new ArgumentException("دسته‌بندی پین معتبر نیست");
        if (category.Operator != selection.Operator)
            throw new ArgumentException("دسته‌بندی پین با اپراتور انتخابی مطابقت ندارد");

        var cost = checked(category.AmountMinor * selection.PinCount);
        return new(
            category.Code,
            category.Description,
            1003,
            0,
            cost,
            JsonSerializer.Serialize(category));
    }

    private sealed record ResolvedSelection(
        string ProductId,
        string Caption,
        int ProductCategory,
        int PayBill,
        long CostMinor,
        string SnapshotJson);
}

public sealed record ChargeSelection(
    ChargeOperator Operator,
    ChargeServiceType ServiceType,
    string DestinationMobileNumber,
    string? ProviderProductId,
    long? RequestedAmountMinor,
    int? PinCategoryId,
    int PinCount);

public sealed record ResolvedChargeQuote(
    string ProviderName,
    string ProductId,
    string Caption,
    int ProductCategory,
    int PayBill,
    string ProductSnapshotJson,
    long ProviderCostMinor,
    Guid? MarkupRuleId,
    decimal MarkupPercent,
    long MarkupFixedMinor,
    long MarkupAmountMinor,
    long FinalAmountMinor,
    DateTime ExpireAt);
