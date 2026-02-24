namespace Refahi.Modules.Wallets.Domain.Enums;

/// <summary>
/// Payment Intent status - state machine enforcement.
/// Storage contract: persisted as SMALLINT. Values must never be renumbered.
/// </summary>
public enum PaymentIntentStatus : short
{
    /// <summary>
    /// Intent is reserved, awaiting capture or release.
    /// </summary>
    Reserved = 1,
    
    /// <summary>
    /// Intent has been captured (terminal state).
    /// </summary>
    Captured = 2,
    
    /// <summary>
    /// Intent has been released/cancelled (terminal state).
    /// </summary>
    Released = 3
}
