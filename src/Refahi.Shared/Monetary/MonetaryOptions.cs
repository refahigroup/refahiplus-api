namespace Refahi.Shared.Monetary;

public sealed class MonetaryOptions
{
    public const string SectionName = "Monetary";

    public string PlatformCurrency { get; init; } = SupportedCurrencies.IRR;
}
