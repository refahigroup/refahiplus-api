using Dapper;
using Npgsql;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;

public sealed class PaymentIntegrityRepairer : IPaymentIntegrityRepairer
{
    private readonly string _connectionString;
    public PaymentIntegrityRepairer(string connectionString) => _connectionString = connectionString;

    public async Task<OrphanHoldRepairResult> RepairOrphanHoldAsync(
        Guid intentId, Guid expectedOrderId, bool dryRun, string idempotencyKey, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var intent = await connection.QuerySingleOrDefaultAsync<IntentRow>(new CommandDefinition(@"
select intent_id as IntentId, order_id as OrderId, status as Status, amount_minor as AmountMinor, currency as Currency
from wallets.payment_intents where intent_id=@IntentId for update", new { IntentId = intentId }, transaction, cancellationToken: ct))
            ?? throw new InvalidOperationException("رزرو پرداخت یافت نشد");
        if (intent.OrderId != expectedOrderId) throw new InvalidOperationException("رزرو با سفارش مورد انتظار مطابقت ندارد");

        var allocations = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "select count(*) from wallets.payment_intent_allocations where intent_id=@IntentId",
            new { IntentId = intentId }, transaction, cancellationToken: ct));
        var holds = (await connection.QueryAsync<HoldRow>(new CommandDefinition(@"
select ledger_entry_id as LedgerEntryId, wallet_id as WalletId, amount_minor as AmountMinor, currency as Currency
from wallets.ledger_entries
where external_reference=@Reference and operation_type=2 and entry_type=3", new { Reference = $"intent:{intentId}" }, transaction, cancellationToken: ct))).ToList();
        var releases = (await connection.QueryAsync<Guid>(new CommandDefinition(@"
select ledger_entry_id from wallets.ledger_entries
where related_entry_id = any(@HoldIds) and operation_type=4 and entry_type=4",
            new { HoldIds = holds.Select(x => x.LedgerEntryId).ToArray() }, transaction, cancellationToken: ct))).ToList();

        if (intent.Status == 3 && releases.Count == 1)
        {
            await transaction.CommitAsync(ct);
            return new(intentId, intent.OrderId, holds.SingleOrDefault()?.WalletId, holds.SingleOrDefault()?.LedgerEntryId,
                releases[0], intent.AmountMinor, dryRun, "AlreadyRepaired", null, null, null, null);
        }
        if (intent.Status != 1 || allocations != 0 || holds.Count != 1 || releases.Count != 0 || holds[0].AmountMinor != intent.AmountMinor)
            throw new InvalidOperationException("الگوی ناسازگاری رزرو با Hold یتیم مورد انتظار مطابقت ندارد؛ عملیات متوقف شد");

        var hold = holds[0];
        await connection.ExecuteAsync(new CommandDefinition(
            "select pg_advisory_xact_lock(hashtext(@WalletId::text));", new { hold.WalletId }, transaction, cancellationToken: ct));
        var before = await connection.QuerySingleAsync<BalanceRow>(new CommandDefinition(@"
select available_minor as AvailableMinor, pending_minor as PendingMinor
from wallets.wallet_balances where wallet_id=@WalletId for update", new { hold.WalletId }, transaction, cancellationToken: ct));

        if (dryRun)
        {
            await transaction.RollbackAsync(ct);
            return new(intentId, intent.OrderId, hold.WalletId, hold.LedgerEntryId, null, hold.AmountMinor,
                true, "Repairable", before.AvailableMinor, before.PendingMinor, before.AvailableMinor, before.PendingMinor);
        }

        var releaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await connection.ExecuteAsync(new CommandDefinition(@"
insert into wallets.ledger_entries
(ledger_entry_id,wallet_id,operation_id,operation_type,entry_type,amount_minor,currency,effective_at,created_at,related_entry_id,relation_type,external_reference,metadata)
values (@ReleaseId,@WalletId,@OperationId,4,4,@AmountMinor,@Currency,@Now,@Now,@HoldId,3,@ExternalReference,cast(@Metadata as jsonb));
update wallets.payment_intents set status=3,released_at=@Now where intent_id=@IntentId;",
            new
            {
                ReleaseId = releaseId, hold.WalletId, OperationId = Guid.NewGuid(), hold.AmountMinor, hold.Currency,
                Now = now, HoldId = hold.LedgerEntryId, ExternalReference = $"repair:intent:{intentId}",
                Metadata = System.Text.Json.JsonSerializer.Serialize(new { repair = "orphan-hold", idempotencyKey }), IntentId = intentId
            }, transaction, cancellationToken: ct));

        var computed = await connection.QuerySingleAsync<ComputedRow>(new CommandDefinition(@"
select
 coalesce(sum(amount_minor) filter(where operation_type=1),0)
 - coalesce(sum(amount_minor) filter(where operation_type=2),0)
 + coalesce(sum(amount_minor) filter(where operation_type=4),0)
 + coalesce(sum(amount_minor) filter(where operation_type=5),0) as AvailableMinor,
 coalesce(sum(amount_minor) filter(where operation_type=2),0)
 - coalesce(sum(amount_minor) filter(where operation_type=3),0)
 - coalesce(sum(amount_minor) filter(where operation_type=4),0) as PendingMinor
from wallets.ledger_entries where wallet_id=@WalletId", new { hold.WalletId }, transaction, cancellationToken: ct));
        await connection.ExecuteAsync(new CommandDefinition(@"
update wallets.wallet_balances set available_minor=@AvailableMinor,pending_minor=@PendingMinor,
 last_ledger_entry_id=@ReleaseId,updated_at=@Now,version=version+1 where wallet_id=@WalletId",
            new { computed.AvailableMinor, computed.PendingMinor, ReleaseId = releaseId, Now = now, hold.WalletId }, transaction, cancellationToken: ct));
        await transaction.CommitAsync(ct);

        return new(intentId, intent.OrderId, hold.WalletId, hold.LedgerEntryId, releaseId, hold.AmountMinor,
            false, "Repaired", before.AvailableMinor, before.PendingMinor, computed.AvailableMinor, computed.PendingMinor);
    }

    private sealed record IntentRow(Guid IntentId, Guid OrderId, short Status, long AmountMinor, string Currency);
    private sealed record HoldRow(Guid LedgerEntryId, Guid WalletId, long AmountMinor, string Currency);
    private sealed record BalanceRow(long AvailableMinor, long PendingMinor);
    private sealed record ComputedRow(long AvailableMinor, long PendingMinor);
}
