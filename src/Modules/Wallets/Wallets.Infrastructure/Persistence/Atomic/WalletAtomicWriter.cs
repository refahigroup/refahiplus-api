using Dapper;
using Npgsql;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wallets.Application.Contracts.Exceptions;
using Wallets.Application.Contracts.Infrastructure;
using Wallets.Domain.Enums;

namespace Wallets.Infrastructure.Persistence.Atomic;

/// <summary>
/// Infrastructure: Atomic execution of wallet write operations.
/// 
/// Responsibilities:
/// - DB connection & transaction management
/// - Advisory locks
/// - SQL execution (idempotency, ledger, balance)
/// - Constraint validation (throws domain exceptions)
/// - Returns structured outcome (NO business interpretation)
/// 
/// Does NOT:
/// - Make business decisions about what to return to user
/// - Build API responses
/// - Normalize inputs (receives pre-normalized data)
/// 
/// This is pure infrastructure execution.
/// </summary>
public sealed class WalletAtomicWriter : IWalletAtomicWriter
{
    private readonly string _connectionString;

    public WalletAtomicWriter(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<WalletTopUpAtomicResult> ExecuteTopUpAsync(
        Guid walletId,
        string idempotencyKey,
        long amountMinor,
        string currency,
        string? externalReference,
        string? metadataJson,
        CancellationToken ct)
    {
        var requestHash = ComputeRequestHash(walletId, amountMinor, currency, metadataJson, externalReference);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // 0) Idempotency: insert PENDING if absent (BEFORE lock to avoid blocking followers)
        var operationId = Guid.NewGuid();
        var idempotencyId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var insertedOperationId = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            Sql.IdempotencyInsertPendingReturningOperationId,
            new
            {
                IdempotencyId = idempotencyId,
                WalletId = walletId,
                IdempotencyKey = idempotencyKey,
                OperationId = operationId,
                OperationType = (short)OperationType.TopUp,
                RequestHash = requestHash,
                StatusPending = (short)IdempotencyStatus.Pending,
                CreatedAt = now
            },
            tx,
            cancellationToken: ct));

        var isNewIdempotency = insertedOperationId.HasValue;

        // 2) Read idempotency state (inserted or pre-existing)
        var idem = await conn.QuerySingleOrDefaultAsync<IdempotencyRow>(
            new CommandDefinition(
                Sql.IdempotencySelectForWalletKey,
                new
                {
                    WalletId = walletId,
                    IdempotencyKey = idempotencyKey,
                    OperationType = (short)OperationType.TopUp
                },
                tx,
                cancellationToken: ct));

        if (idem is null)
            throw new InvalidOperationException("Idempotency row could not be loaded.");

        if (!idem.RequestHash.SequenceEqual(requestHash))
            throw new IdempotencyKeyConflictException();

        // 2A) Already completed - return cached result (no lock needed)
        if ((IdempotencyStatus)idem.Status == IdempotencyStatus.Completed)
        {
            var completedAt = idem.CompletedAt ?? now;
            var ledgerId = idem.ResultLedgerEntryIds is { Length: > 0 } ? idem.ResultLedgerEntryIds[0] : Guid.Empty;
            var balance = idem.ResultBalanceAvailableMinor ?? 0;

            await tx.CommitAsync(ct);

            return new WalletTopUpAtomicResult(
                Outcome: TopUpOutcome.CompletedCached,
                OperationId: idem.OperationId,
                LedgerEntryId: ledgerId,
                AvailableBalanceMinor: balance,
                CompletedAt: completedAt);
        }

        // 2B) PENDING (new or existing) - attempt try-lock to determine leader
        if ((IdempotencyStatus)idem.Status == IdempotencyStatus.Pending)
        {
            var lockAcquired = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
                Sql.AdvisoryLock,
                new { WalletId = walletId },
                tx,
                cancellationToken: ct
            ));

            if (!lockAcquired)
            {
                // Follower: another transaction holds the lock, return IN_PROGRESS without blocking
                await tx.CommitAsync(ct);

                return new WalletTopUpAtomicResult(
                    Outcome: TopUpOutcome.InProgress,
                    OperationId: idem.OperationId,
                    LedgerEntryId: null,
                    AvailableBalanceMinor: 0,
                    CompletedAt: now);
            }

            // Leader: acquired lock, re-check idempotency (may have been completed by another tx)
            idem = await conn.QuerySingleOrDefaultAsync<IdempotencyRow>(
                new CommandDefinition(
                    Sql.IdempotencySelectForWalletKey,
                    new
                    {
                        WalletId = walletId,
                        IdempotencyKey = idempotencyKey,
                        OperationType = (short)OperationType.TopUp
                    },
                    tx,
                    cancellationToken: ct));

            if (idem is null)
                throw new InvalidOperationException("Idempotency row disappeared after lock acquisition.");

            if ((IdempotencyStatus)idem.Status == IdempotencyStatus.Completed)
            {
                // Completed while we were acquiring lock
                var completedAt = idem.CompletedAt ?? now;
                var ledgerId = idem.ResultLedgerEntryIds is { Length: > 0 } ? idem.ResultLedgerEntryIds[0] : Guid.Empty;
                var balance = idem.ResultBalanceAvailableMinor ?? 0;

                await tx.CommitAsync(ct);

                return new WalletTopUpAtomicResult(
                    Outcome: TopUpOutcome.CompletedCached,
                    OperationId: idem.OperationId,
                    LedgerEntryId: ledgerId,
                    AvailableBalanceMinor: balance,
                    CompletedAt: completedAt);
            }

            // Still PENDING under lock - this request is the leader, continue to business logic
        }

        // 4) Validate wallet constraints (throws domain exceptions)
        var wallet = await conn.QuerySingleOrDefaultAsync<WalletRow>(new CommandDefinition(
            Sql.WalletSelectForValidation,
            new { WalletId = walletId },
            tx,
            cancellationToken: ct));

        if (wallet is null)
            throw new WalletNotFoundException(walletId);

        // Currency already normalized by Application, but validate match
        if (!string.Equals(wallet.Currency, currency, StringComparison.Ordinal))
            throw new WalletCurrencyMismatchException(wallet.Currency, currency);

        if ((WalletStatus)wallet.Status != WalletStatus.Active)
            throw new WalletOperationNotAllowedException(((WalletStatus)wallet.Status).ToString());

        // Use the persisted operation_id (stable across retries)
        var effectiveOperationId = idem.OperationId;

        // 5) Insert ledger entry (append-only)
        var ledgerEntryId = Guid.NewGuid();
        var effectiveAt = now;

        await conn.ExecuteAsync(new CommandDefinition(
            Sql.LedgerInsert,
            new
            {
                LedgerEntryId = ledgerEntryId,
                WalletId = walletId,
                OperationId = effectiveOperationId,
                OperationType = (short)OperationType.TopUp,
                EntryType = (short)EntryType.Credit,
                AmountMinor = amountMinor,
                Currency = currency,
                EffectiveAt = effectiveAt,
                CreatedAt = now,
                ExternalReference = externalReference,
                MetadataJson = metadataJson
            },
            tx,
            cancellationToken: ct));

        // 6) Balance projection UPSERT (version++)
        var upserted = await conn.ExecuteAsync(new CommandDefinition(
            Sql.BalanceUpsert,
            new
            {
                WalletId = walletId,
                DeltaAvailableMinor = amountMinor,
                Currency = currency,
                LastLedgerEntryId = ledgerEntryId,
                UpdatedAt = now
            },
            tx,
            cancellationToken: ct));

        if (upserted == 0)
            throw new WalletCurrencyMismatchException(wallet.Currency, currency);

        var available = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            Sql.BalanceSelect,
            new { WalletId = walletId },
            tx,
            cancellationToken: ct));

        // 7) Mark idempotency as COMPLETED + record result
        var completed = await conn.ExecuteAsync(new CommandDefinition(
            Sql.IdempotencyComplete,
            new
            {
                StatusCompleted = (short)IdempotencyStatus.Completed,
                StatusPending = (short)IdempotencyStatus.Pending,
                WalletId = walletId,
                IdempotencyKey = idempotencyKey,
                OperationType = (short)OperationType.TopUp,
                LedgerEntryId = ledgerEntryId,
                AvailableBalanceMinor = available,
                CompletedAt = now
            },
            tx,
            cancellationToken: ct));

        if (completed != 1)
            throw new InvalidOperationException("Idempotency completion update failed.");

        await tx.CommitAsync(ct);

        // Return structured outcome (Application will interpret)
        return new WalletTopUpAtomicResult(
            Outcome: TopUpOutcome.Completed,
            OperationId: effectiveOperationId,
            LedgerEntryId: ledgerEntryId,
            AvailableBalanceMinor: available,
            CompletedAt: now);
    }

    private static byte[] ComputeRequestHash(Guid walletId, long amountMinor, string currency, string? metadataJson, string? externalReference)
    {
        // Canonical payload. Must be stable and ordering-independent.
        var canonical = string.Join("|", new[]
        {
            walletId.ToString("D"),
            amountMinor.ToString(),
            currency,
            metadataJson ?? string.Empty,
            externalReference ?? string.Empty
        });

        return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
    }

    private enum IdempotencyStatus : short
    {
        Pending = 1,
        Completed = 2
    }

    private sealed record IdempotencyRow(
        Guid IdempotencyId,
        Guid WalletId,
        string IdempotencyKey,
        Guid OperationId,
        short Status,
        byte[] RequestHash,
        Guid[] ResultLedgerEntryIds,
        long? ResultBalanceAvailableMinor,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt);

    private sealed record WalletRow(Guid WalletId, string Currency, short Status);

    public static class Sql
    {
        // REQUIRED EVIDENCE: Exact SQL strings (unchanged from previous implementation)

        public const string AdvisoryLock = @"select pg_try_advisory_xact_lock(hashtext(@WalletId::text));";

        public const string IdempotencyInsertPendingReturningOperationId = @"
insert into wallets.idempotency_keys (
  idempotency_id,
  wallet_id,
  idempotency_key,
  operation_id,
  operation_type,
  request_hash,
  status,
  result_ledger_entry_ids,
  result_balance_available_minor,
  created_at,
  completed_at,
  error_code,
  error_message
)
values (
  @IdempotencyId,
  @WalletId,
  @IdempotencyKey,
  @OperationId,
  @OperationType,
  @RequestHash,
  @StatusPending,
  array[]::uuid[],
  null,
  @CreatedAt,
  null,
  null,
  null
)
on conflict (wallet_id, idempotency_key, operation_type) do nothing
returning operation_id;
";

        public const string IdempotencySelectForWalletKey = @"
select
  idempotency_id as IdempotencyId,
  wallet_id as WalletId,
  idempotency_key as IdempotencyKey,
  operation_id as OperationId,
  status as Status,
  request_hash as RequestHash,
  result_ledger_entry_ids as ResultLedgerEntryIds,
  result_balance_available_minor as ResultBalanceAvailableMinor,
  created_at as CreatedAt,
  completed_at as CompletedAt
from wallets.idempotency_keys
where wallet_id = @WalletId and idempotency_key = @IdempotencyKey and operation_type = @OperationType;
";

        public const string WalletSelectForValidation = @"
select wallet_id as WalletId, currency as Currency, status as Status
from wallets.wallets
where wallet_id = @WalletId;
";

        public const string LedgerInsert = @"
insert into wallets.ledger_entries (
  ledger_entry_id,
  wallet_id,
  operation_id,
  operation_type,
  entry_type,
  amount_minor,
  currency,
  effective_at,
  created_at,
  related_entry_id,
  relation_type,
  external_reference,
  metadata
)
values (
  @LedgerEntryId,
  @WalletId,
  @OperationId,
  @OperationType,
  @EntryType,
  @AmountMinor,
  @Currency,
  @EffectiveAt,
  @CreatedAt,
  null,
  0,
  @ExternalReference,
  case when @MetadataJson is null then null else cast(@MetadataJson as jsonb) end
);
";

        public const string BalanceUpsert = @"
insert into wallets.wallet_balances (
  wallet_id,
  available_minor,
  pending_minor,
  currency,
  last_ledger_entry_id,
  updated_at,
  version
)
values (
  @WalletId,
  @DeltaAvailableMinor,
  0,
  @Currency,
  @LastLedgerEntryId,
  @UpdatedAt,
  1
)
on conflict (wallet_id) do update
set
  available_minor = wallets.wallet_balances.available_minor + excluded.available_minor,
  pending_minor = wallets.wallet_balances.pending_minor,
  updated_at = excluded.updated_at,
  version = wallets.wallet_balances.version + 1,
  last_ledger_entry_id = excluded.last_ledger_entry_id
where wallets.wallet_balances.currency = excluded.currency;
";

        public const string BalanceSelect = @"
select available_minor as AvailableMinor
from wallets.wallet_balances
where wallet_id = @WalletId;
";

        public const string IdempotencyComplete = @"
update wallets.idempotency_keys
set
  status = @StatusCompleted,
  result_ledger_entry_ids = array[@LedgerEntryId],
  result_balance_available_minor = @AvailableBalanceMinor,
  completed_at = @CompletedAt,
  error_code = null,
  error_message = null
where wallet_id = @WalletId and idempotency_key = @IdempotencyKey and operation_type = @OperationType and status = @StatusPending;
";
    }
}
