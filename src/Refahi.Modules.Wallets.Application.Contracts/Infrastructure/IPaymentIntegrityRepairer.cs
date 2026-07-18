using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Contracts.Infrastructure;

public interface IPaymentIntegrityRepairer
{
    Task<OrphanHoldRepairResult> RepairOrphanHoldAsync(
        Guid intentId, Guid expectedOrderId, bool dryRun, string idempotencyKey, CancellationToken ct);
}

public sealed record OrphanHoldRepairResult(
    Guid IntentId, Guid OrderId, Guid? WalletId, Guid? HoldLedgerEntryId,
    Guid? ReleaseLedgerEntryId, long AmountMinor, bool DryRun, string Status,
    long? AvailableBefore, long? PendingBefore, long? AvailableAfter, long? PendingAfter);
