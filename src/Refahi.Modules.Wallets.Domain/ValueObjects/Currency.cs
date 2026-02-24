using Refahi.Modules.Wallets.Domain.Exceptions;
using System;
using System.Collections.Generic;

namespace Refahi.Modules.Wallets.Domain.ValueObjects;

/// <summary>
/// Value Object: Currency (ISO-4217 alpha-3).
/// Immutable, self-validating, prevents primitive obsession.
/// </summary>
public sealed record Currency
{
    public string Code { get; }

    private Currency(string code)
    {
        Code = code;
    }

    /// <summary>
    /// Factory: Parse and validate ISO-4217 currency code.
    /// </summary>
    public static Currency Of(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new CurrencyRequiredWalletDomainException("Currency is required.");

        code = code.Trim().ToUpperInvariant();

        if (code.Length != 3 || !IsAlpha(code))
            throw new CurrencyInvalidWalletDomainException("Currency must be ISO-4217 alpha-3.");

        return new Currency(code);
    }

    /// <summary>
    /// Implicit conversion to string for persistence/serialization.
    /// </summary>
    public static implicit operator string(Currency currency) => currency.Code;

    /// <summary>
    /// Explicit conversion from string (requires validation).
    /// </summary>
    public static explicit operator Currency(string code) => Of(code);

    public override string ToString() => Code;

    private static bool IsAlpha(string s)
    {
        foreach (var ch in s)
        {
            if (ch < 'A' || ch > 'Z')
                return false;
        }
        return true;
    }

    /// <summary>
    /// Common currencies (helper).
    /// </summary>
    public static Currency USD => new("USD");
    public static Currency EUR => new("EUR");
    public static Currency IRR => new("IRR");
}
