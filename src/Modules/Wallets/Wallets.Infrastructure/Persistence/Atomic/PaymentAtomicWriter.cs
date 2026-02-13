using Dapper;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wallets.Application.Contracts.Exceptions;
using Wallets.Application.Contracts.Infrastructure;
using Wallets.Domain.Enums;

namespace Wallets.Infrastructure.Persistence.Atomic;

/// <summary>
/// Infrastructure: Atomic execution of payment intent operations.
/// 
/// Responsibilities:
/// - DB connection & transaction management
/// - Multi-wallet advisory locks (deadlock avoidance via sorting)
/// - SQL execution (idempotency, intents, ledger, balance)
/// - State machine enforcement
/// - Returns structured outcomes
/// 
/// Critical: Multi-wallet operations lock wallets in sorted order by wallet_id.
/// </summary>
public sealed class PaymentAtomicWriter : IPaymentAtomicWriter
{
    private readonly string _connectionString;

    public PaymentAtomicWriter(string connectionString)
    {
        _connectionString = connectionString;
    }

    // ================================================================
    // CREATE INTENT (RESERVE)
    // ================================================================
    public async Task<CreateIntentAtomicResult> ExecuteCreateIntentAsync(
        Guid orderId,
        string idempotencyKey,
        long totalAmountMinor,
        string currency,
        List<AllocationInput> allocations,
        string? metadataJson,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var intentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // 0) Idempotency: Check if intent already exists for this order + key
        var existing = await conn.QuerySingleOrDefaultAsync<IntentIdempotencyRow>(
            new CommandDefinition(
                Sql.IntentIdempotencySelect,
                new { OrderId = orderId, IdempotencyKey = idempotencyKey },
                tx,
                cancellationToken: ct));

        if (existing is not null)
        {
            // Intent already created - return cached
            var cachedIntent = await LoadIntentWithAllocations(conn, tx, existing.IntentId, ct);
            await tx.CommitAsync(ct);

            return new CreateIntentAtomicResult(
                Outcome: CreateIntentOutcome.CreatedCached,
                IntentId: cachedIntent.IntentId,
                OrderId: cachedIntent.OrderId,
                AmountMinor: cachedIntent.AmountMinor,
                Currency: cachedIntent.Currency,
                Allocations: cachedIntent.Allocations,
                CreatedAt: cachedIntent.CreatedAt);
        }

        // 1) Sort wallet IDs to prevent deadlocks
        var sortedWalletIds = allocations.Select(a => a.WalletId).OrderBy(id => id).ToList();

        // 2) Acquire locks on all wallets (in sorted order)
        foreach (var walletId in sortedWalletIds)
        {
            var lockAcquired = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
                Sql.AdvisoryLock,
                new { WalletId = walletId },
                tx,
                cancellationToken: ct
            ));

            if (!lockAcquired)
            {
                // Follower: another tx holds lock on this wallet, return IN_PROGRESS
                await tx.CommitAsync(ct);
                return new CreateIntentAtomicResult(
                    Outcome: CreateIntentOutcome.InProgress,
                    IntentId: intentId,
                    OrderId: orderId,
                    AmountMinor: totalAmountMinor,
                    Currency: currency,
                    Allocations: allocations.Select(a => new AllocationOutput(a.WalletId, a.AmountMinor)).ToList(),
                    CreatedAt: now);
            }
        }

        // 3) Validate all wallets (existence, currency match, status active, sufficient available balance)
        foreach (var allocation in allocations)
        {
            var wallet = await conn.QuerySingleOrDefaultAsync<WalletRow>(new CommandDefinition(
                Sql.WalletSelectForValidation,
                new { WalletId = allocation.WalletId },
                tx,
                cancellationToken: ct));

            if (wallet is null)
                throw new WalletNotFoundException(allocation.WalletId);

            if (!string.Equals(wallet.Currency, currency, StringComparison.Ordinal))
                throw new WalletCurrencyMismatchException(wallet.Currency, currency);

            if ((WalletStatus)wallet.Status != WalletStatus.Active)
                throw new WalletOperationNotAllowedException(((WalletStatus)wallet.Status).ToString());

            // Check available balance
            var balance = await conn.QuerySingleOrDefaultAsync<BalanceRow>(new CommandDefinition(
                Sql.BalanceSelect,
                new { WalletId = allocation.WalletId },
                tx,
                cancellationToken: ct));

            var availableMinor = balance?.AvailableMinor ?? 0;
            if (availableMinor < allocation.AmountMinor)
                throw new InsufficientFundsException(allocation.WalletId, availableMinor, allocation.AmountMinor);
        }

        // 4) Insert payment_intents record
        await conn.ExecuteAsync(new CommandDefinition(
            Sql.IntentInsert,
            new
            {
                IntentId = intentId,
                OrderId = orderId,
                IdempotencyKey = idempotencyKey,
                Status = (short)PaymentIntentStatus.Reserved,
                AmountMinor = totalAmountMinor,
                Currency = currency,
                CreatedAt = now,
                MetadataJson = metadataJson
            },
            tx,
            cancellationToken: ct));

        // 5) Insert allocations + create HOLD ledger entries + update balances
        var sequence = 0;
        var allocationOutputs = new List<AllocationOutput>();

        foreach (var allocation in allocations)
        {
            var allocationId = Guid.NewGuid();
            var holdLedgerEntryId = Guid.NewGuid();
            var operationId = Guid.NewGuid();

            // Insert allocation record
            await conn.ExecuteAsync(new CommandDefinition(
                Sql.AllocationInsert,
                new
                {
                    AllocationId = allocationId,
                    IntentId = intentId,
                    WalletId = allocation.WalletId,
                    AmountMinor = allocation.AmountMinor,
                    Sequence = sequence++
                },
                tx,
                cancellationToken: ct));

            // Insert HOLD ledger entry (reduces available balance)
            await conn.ExecuteAsync(new CommandDefinition(
                Sql.LedgerInsertHold,
                new
                {
                    LedgerEntryId = holdLedgerEntryId,
                    WalletId = allocation.WalletId,
                    OperationId = operationId,
                    OperationType = (short)OperationType.Reserve,
                    EntryType = (short)EntryType.Hold,
                    AmountMinor = allocation.AmountMinor,
                    Currency = currency,
                    EffectiveAt = now,
                    CreatedAt = now,
                    ExternalReference = $"intent:{intentId}",
                    MetadataJson = metadataJson
                },
                tx,
                cancellationToken: ct));

            // Update balance (reduce available, increase pending/held)
            await conn.ExecuteAsync(new CommandDefinition(
                Sql.BalanceUpdateHold,
                new
                {
                    WalletId = allocation.WalletId,
                    AmountMinor = allocation.AmountMinor,
                    LastLedgerEntryId = holdLedgerEntryId,
                    UpdatedAt = now
                },
                tx,
                cancellationToken: ct));

            allocationOutputs.Add(new AllocationOutput(allocation.WalletId, allocation.AmountMinor));
        }

        await tx.CommitAsync(ct);

        return new CreateIntentAtomicResult(
            Outcome: CreateIntentOutcome.Created,
            IntentId: intentId,
            OrderId: orderId,
            AmountMinor: totalAmountMinor,
            Currency: currency,
            Allocations: allocationOutputs,
            CreatedAt: now);
    }

    // ================================================================
    // HELPER: Load intent with allocations
    // ================================================================
    private static async Task<IntentWithAllocationsRow> LoadIntentWithAllocations(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid intentId,
        CancellationToken ct)
    {
        var intent = await conn.QuerySingleAsync<IntentRow>(new CommandDefinition(
            Sql.IntentSelect,
            new { IntentId = intentId },
            tx,
            cancellationToken: ct));

        var allocations = await conn.QueryAsync<AllocationRow>(new CommandDefinition(
            Sql.AllocationsSelectByIntent,
            new { IntentId = intentId },
            tx,
            cancellationToken: ct));

        return new IntentWithAllocationsRow(
            intent.IntentId,
            intent.OrderId,
            intent.AmountMinor,
            intent.Currency,
            allocations.Select(a => new AllocationOutput(a.WalletId, a.AmountMinor)).ToList(),
            intent.CreatedAt);
    }

    // ================================================================
    // (CAPTURE and RELEASE methods to be continued in next message due to size)
    // Placeholder stubs for compilation:
    // ================================================================
    public Task<CaptureIntentAtomicResult> ExecuteCaptureIntentAsync(Guid intentId, string idempotencyKey, CancellationToken ct)
    {
        throw new NotImplementedException("To be implemented");
    }

    public Task<ReleaseIntentAtomicResult> ExecuteReleaseIntentAsync(Guid intentId, string idempotencyKey, CancellationToken ct)
    {
        throw new NotImplementedException("To be implemented");
    }

    // ================================================================
    // RECORD TYPES (Internal DTOs)
    // ================================================================
    private sealed record IntentIdempotencyRow(Guid IntentId);
    private sealed record IntentRow(Guid IntentId, Guid OrderId, long AmountMinor, string Currency, DateTimeOffset CreatedAt);
    private sealed record AllocationRow(Guid WalletId, long AmountMinor);
    private sealed record IntentWithAllocationsRow(Guid IntentId, Guid OrderId, long AmountMinor, string Currency, List<AllocationOutput> Allocations, DateTimeOffset CreatedAt);
    private sealed record WalletRow(Guid WalletId, string Currency, short Status);
    private sealed record BalanceRow(long AvailableMinor, long PendingMinor);

    // ================================================================
    // SQL CONSTANTS (Partial - will add Capture/Release SQLs next)
    // ================================================================
    private static class Sql
    {
        public const string AdvisoryLock = @"select pg_try_advisory_xact_lock(hashtext(@WalletId::text));";

        public const string IntentIdempotencySelect = @"
select intent_id as IntentId
from wallets.payment_intents
where order_id = @OrderId and idempotency_key = @IdempotencyKey;
";

        public const string IntentSelect = @"
select intent_id as IntentId, order_id as OrderId, amount_minor as AmountMinor, 
       currency as Currency, created_at as CreatedAt
from wallets.payment_intents
where intent_id = @IntentId;
";

        public const string AllocationsSelectByIntent = @"
select wallet_id as WalletId, amount_minor as AmountMinor
from wallets.payment_intent_allocations
where intent_id = @IntentId
order by sequence;
";

        public const string WalletSelectForValidation = @"
select wallet_id as WalletId, currency as Currency, status as Status
from wallets.wallets
where wallet_id = @WalletId;
";

        public const string BalanceSelect = @"
select available_minor as AvailableMinor, pending_minor as PendingMinor
from wallets.wallet_balances
where wallet_id = @WalletId;
";

        public const string IntentInsert = @"
insert into wallets.payment_intents (intent_id, order_id, idempotency_key, status, amount_minor, currency, created_at, metadata)
values (@IntentId, @OrderId, @IdempotencyKey, @Status, @AmountMinor, @Currency, @CreatedAt, 
        case when @MetadataJson is null then null else cast(@MetadataJson as jsonb) end);
";

        public const string AllocationInsert = @"
insert into wallets.payment_intent_allocations (allocation_id, intent_id, wallet_id, amount_minor, sequence)
values (@AllocationId, @IntentId, @WalletId, @AmountMinor, @Sequence);
";

        public const string LedgerInsertHold = @"
insert into wallets.ledger_entries 
(ledger_entry_id, wallet_id, operation_id, operation_type, entry_type, amount_minor, currency, 
 effective_at, created_at, external_reference, metadata)
values 
(@LedgerEntryId, @WalletId, @OperationId, @OperationType, @EntryType, @AmountMinor, @Currency,
 @EffectiveAt, @CreatedAt, @ExternalReference, 
 case when @MetadataJson is null then null else cast(@MetadataJson as jsonb) end);
";

        public const string BalanceUpdateHold = @"
update wallets.wallet_balances
set 
  available_minor = available_minor - @AmountMinor,
  pending_minor = pending_minor + @AmountMinor,
  last_ledger_entry_id = @LastLedgerEntryId,
  updated_at = @UpdatedAt,
  version = version + 1
where wallet_id = @WalletId;
";
    }
}
