namespace Refahi.Modules.Wallets.Domain.Enums;

/// <summary>
/// Refund status.
/// Storage contract: persisted as SMALLINT. Values must never be renumbered.
/// </summary>
public enum RefundStatus : short
{
    /// <summary>
    /// Refund completed successfully.
    /// </summary>
    Completed = 1
}
