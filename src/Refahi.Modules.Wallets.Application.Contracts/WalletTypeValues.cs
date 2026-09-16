namespace Refahi.Modules.Wallets.Application.Contracts;

/// <summary>Stable persisted wallet-type values exposed for cross-module policy checks.</summary>
public static class WalletTypeValues
{
    public const short System = 1;
    public const short User = 2;
    public const short Provider = 3;
    public const short OrgCredit = 4;
}
