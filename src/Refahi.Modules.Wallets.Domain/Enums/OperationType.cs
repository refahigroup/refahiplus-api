namespace Refahi.Modules.Wallets.Domain.Enums;

/// <summary>
/// Storage contract: persisted as SMALLINT. Values must never be renumbered.
/// </summary>
public enum OperationType : short
{
    TopUp = 1,
    Reserve = 2,
    Payment = 3,
    Release = 4,
    Refund = 5
}
