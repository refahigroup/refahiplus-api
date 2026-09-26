using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Tests;

internal static class MerchantTestFactory
{
    public static Merchant Create(Guid identitySeed, string name, DateTimeOffset now)
    {
        var nationalId = (BitConverter.ToUInt64(identitySeed.ToByteArray(), 0) % 10_000_000_000UL).ToString("D10");
        var merchant = Merchant.Create(MerchantEntityType.Individual, nationalId, name, name, now);
        merchant.BindSettlementWallet(Guid.NewGuid(), now);
        return merchant;
    }
}
