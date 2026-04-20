namespace Refahi.Modules.Wallets.Domain.Enums;

/// <summary>
/// Storage contract: persisted as SMALLINT. Values must never be renumbered.
/// </summary>
public enum WalletType : short
{
    System = 1,
    User = 2,       // REFAHI — personal wallet
    Provider = 3,
    OrgCredit = 4   // ORG_CREDIT — organisation credit wallet
}
