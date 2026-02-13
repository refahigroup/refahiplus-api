using System;

namespace Wallets.Infrastructure.Persistence.Models;

public sealed class WalletRecord
{
    public Guid WalletId { get; set; }
    public short OwnerType { get; set; }
    public Guid OwnerId { get; set; }
    public short WalletType { get; set; }
    public short Status { get; set; }
    public string Currency { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? Metadata { get; set; }
}
