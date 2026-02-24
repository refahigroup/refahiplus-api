using System;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Models;

public class WalletBalanceModel
{
    public Guid WalletId { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = null!;
    public long Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
