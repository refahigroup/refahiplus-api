using System;
using System.Collections.Generic;
using System.Linq;

namespace Refahi.Modules.Wallets.Domain.ValueObjects;

/// <summary>
/// Value Object: Payment Allocation (wallet_id + amount).
/// Used for multi-wallet payment splitting.
/// Immutable, validates amount > 0.
/// </summary>
public sealed record PaymentAllocation
{
    public Guid WalletId { get; }
    public Money Amount { get; }

    private PaymentAllocation(Guid walletId, Money amount)
    {
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId cannot be empty.", nameof(walletId));

        WalletId = walletId;
        Amount = amount ?? throw new ArgumentNullException(nameof(amount));
    }

    /// <summary>
    /// Factory: Create allocation with validation.
    /// </summary>
    public static PaymentAllocation Of(Guid walletId, Money amount)
    {
        return new PaymentAllocation(walletId, amount);
    }

    /// <summary>
    /// Factory: Create allocation with currency string.
    /// </summary>
    public static PaymentAllocation Of(Guid walletId, long amountMinor, string currencyCode)
    {
        return new PaymentAllocation(walletId, Money.Of(amountMinor, currencyCode));
    }

    /// <summary>
    /// Validate that all allocations have the same currency.
    /// </summary>
    public static void ValidateSameCurrency(IEnumerable<PaymentAllocation> allocations)
    {
        var allocs = allocations.ToList();
        if (allocs.Count == 0)
            throw new ArgumentException("Allocations cannot be empty.", nameof(allocations));

        var firstCurrency = allocs[0].Amount.Currency;
        if (allocs.Any(a => a.Amount.Currency != firstCurrency))
            throw new InvalidOperationException("All allocations must have the same currency.");
    }

    /// <summary>
    /// Validate that sum of allocations equals expected total.
    /// </summary>
    public static void ValidateSum(IEnumerable<PaymentAllocation> allocations, Money expectedTotal)
    {
        var allocs = allocations.ToList();
        if (allocs.Count == 0)
            throw new ArgumentException("Allocations cannot be empty.", nameof(allocations));

        ValidateSameCurrency(allocs);

        if (allocs[0].Amount.Currency != expectedTotal.Currency)
            throw new InvalidOperationException($"Allocation currency {allocs[0].Amount.Currency} does not match expected total currency {expectedTotal.Currency}.");

        var sum = allocs.Sum(a => a.Amount.AmountMinor);
        if (sum != expectedTotal.AmountMinor)
            throw new InvalidOperationException($"Sum of allocations ({sum}) does not match expected total ({expectedTotal.AmountMinor}).");
    }

    public override string ToString() => $"{WalletId}: {Amount}";

    public void Deconstruct(out Guid walletId, out Money amount)
    {
        walletId = WalletId;
        amount = Amount;
    }
}
