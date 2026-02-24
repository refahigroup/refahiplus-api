namespace Refahi.Modules.Wallets.Domain.Enums;

/// <summary>
/// Payment status.
/// Storage contract: persisted as SMALLINT. Values must never be renumbered.
/// </summary>
public enum PaymentStatus : short
{
    /// <summary>
    /// Payment completed successfully.
    /// </summary>
    Completed = 1
}
