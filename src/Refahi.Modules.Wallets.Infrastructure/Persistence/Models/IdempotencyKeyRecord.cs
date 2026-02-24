using System;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Models;

public sealed class IdempotencyKeyRecord
{
    public Guid IdempotencyId { get; set; }
    public Guid WalletId { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public Guid OperationId { get; set; }
    public short OperationType { get; set; }
    public byte[] RequestHash { get; set; } = null!;
    public short Status { get; set; }
    public Guid[] ResultLedgerEntryIds { get; set; } = Array.Empty<Guid>();
    public long? ResultBalanceAvailableMinor { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
