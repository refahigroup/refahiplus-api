namespace Refahi.Shared.Monetary;

public readonly record struct CurrencyCode
{
    private CurrencyCode(string value) => Value = value;

    public string Value { get; }

    public static CurrencyCode Parse(string value)
    {
        if (!TryParse(value, out var currency))
            throw new ArgumentException("تنها ارز پشتیبانی‌شده IRR است", nameof(value));

        return currency;
    }

    public static bool TryParse(string? value, out CurrencyCode currency)
    {
        if (SupportedCurrencies.IsSupported(value))
        {
            currency = new CurrencyCode(SupportedCurrencies.IRR);
            return true;
        }

        currency = default;
        return false;
    }

    public override string ToString() => Value;
}
