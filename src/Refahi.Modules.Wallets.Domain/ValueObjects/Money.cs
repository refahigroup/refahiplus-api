using System;

namespace Refahi.Modules.Wallets.Domain.ValueObjects;

/// <summary>
/// Value Object: Money (amount + currency).
/// Immutable, prevents split of amount/currency, enforces business rules.
/// </summary>
public sealed record Money
{
    /// <summary>
    /// Amount in minor units (e.g., cents, fils, rials).
    /// </summary>
    public long AmountMinor { get; }

    public Currency Currency { get; }

    private Money(long amountMinor, Currency currency)
    {
        if (amountMinor <= 0)
            throw new ArgumentException("Money amount must be positive.", nameof(amountMinor));

        AmountMinor = amountMinor;
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
    }

    /// <summary>
    /// Factory: Create money with validation.
    /// </summary>
    public static Money Of(long amountMinor, Currency currency)
    {
        return new Money(amountMinor, currency);
    }

    /// <summary>
    /// Factory: Create money from string currency code.
    /// </summary>
    public static Money Of(long amountMinor, string currencyCode)
    {
        return new Money(amountMinor, Currency.Of(currencyCode));
    }

    /// <summary>
    /// Add two money values (must be same currency).
    /// </summary>
    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot add {Currency} and {other.Currency}.");

        return new Money(AmountMinor + other.AmountMinor, Currency);
    }

    /// <summary>
    /// Subtract two money values (must be same currency).
    /// </summary>
    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot subtract {other.Currency} from {Currency}.");

        var result = AmountMinor - other.AmountMinor;
        if (result <= 0)
            throw new InvalidOperationException("Subtraction would result in zero or negative amount.");

        return new Money(result, Currency);
    }

    /// <summary>
    /// Check if same currency.
    /// </summary>
    public bool IsSameCurrency(Money other) => Currency == other.Currency;

    public override string ToString() => $"{AmountMinor} {Currency}";

    /// <summary>
    /// Deconstruct for pattern matching.
    /// </summary>
    public void Deconstruct(out long amountMinor, out Currency currency)
    {
        amountMinor = AmountMinor;
        currency = Currency;
    }
}
