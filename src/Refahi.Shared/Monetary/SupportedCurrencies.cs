namespace Refahi.Shared.Monetary;

public static class SupportedCurrencies
{
    public const string IRR = "IRR";

    public static bool IsSupported(string? currency) =>
        string.Equals(currency?.Trim(), IRR, StringComparison.OrdinalIgnoreCase);
}
