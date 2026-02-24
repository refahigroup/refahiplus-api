using Dapper;
using Npgsql;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using Refahi.Modules.Wallets.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wallets.Application.Contracts.Exceptions;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;

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
    // CAPTURE INTENT (FINALIZE PAYMENT)
    // ================================================================
    public async Task<CaptureIntentAtomicResult> ExecuteCaptureIntentAsync(
        Guid intentId,
        string idempotencyKey,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var operationType = (short)OperationType.Payment;

        // 1) Resolve idempotency (intent-level operation idempotency)
        var existingIdem = await conn.QuerySingleOrDefaultAsync<IntentOperationIdempotencyRow>(
            new CommandDefinition(
                Sql.IntentOperationIdempotencySelect,
                new { IntentId = intentId, IdempotencyKey = idempotencyKey, OperationType = operationType },
                tx,
                cancellationToken: ct));

        if (existingIdem is not null)
        {
            // 2) If completed → return cached result
            if (existingIdem.Status == 2) // Completed
            {
                var cachedPayment = await LoadPaymentWithAllocations(conn, tx, existingIdem.ResultPaymentId!.Value, ct);
                await tx.CommitAsync(ct);

                return new CaptureIntentAtomicResult(
                    Outcome: CaptureIntentOutcome.CapturedCached,
                    PaymentId: cachedPayment.PaymentId,
                    IntentId: intentId,
                    OrderId: cachedPayment.OrderId,
                    AmountMinor: cachedPayment.AmountMinor,
                    Currency: cachedPayment.Currency,
                    Allocations: cachedPayment.Allocations,
                    CompletedAt: cachedPayment.CompletedAt);
            }

            // 3) If pending and not leader → return IN_PROGRESS
            // (try-lock will be attempted below to determine leadership)
        }
        else
        {
            // Insert pending idempotency record
            await conn.ExecuteAsync(new CommandDefinition(
                Sql.IntentOperationIdempotencyInsert,
                new
                {
                    IdempotencyId = Guid.NewGuid(),
                    IntentId = intentId,
                    IdempotencyKey = idempotencyKey,
                    OperationType = operationType,
                    Status = (short)1, // Pending
                    CreatedAt = now
                },
                tx,
                cancellationToken: ct));
        }

        // 4) Load intent and verify state is RESERVED
        var intent = await conn.QuerySingleOrDefaultAsync<IntentWithStatusRow>(new CommandDefinition(
            Sql.IntentSelectWithStatus,
            new { IntentId = intentId },
            tx,
            cancellationToken: ct));

        if (intent is null)
            throw new PaymentIntentNotFoundException(intentId);

        if ((PaymentIntentStatus)intent.Status == PaymentIntentStatus.Released)
            throw new PaymentIntentStateViolationException("capture", "released");

        if ((PaymentIntentStatus)intent.Status == PaymentIntentStatus.Captured)
        {
            // Already captured - load existing payment as cached result
            var existingPayment = await LoadPaymentByIntent(conn, tx, intentId, ct);
            await tx.CommitAsync(ct);

            return new CaptureIntentAtomicResult(
                Outcome: CaptureIntentOutcome.CapturedCached,
                PaymentId: existingPayment.PaymentId,
                IntentId: intentId,
                OrderId: intent.OrderId,
                AmountMinor: intent.AmountMinor,
                Currency: intent.Currency,
                Allocations: existingPayment.Allocations,
                CompletedAt: existingPayment.CompletedAt);
        }

        // Load allocations
        var allocations = (await conn.QueryAsync<AllocationRow>(new CommandDefinition(
            Sql.AllocationsSelectByIntent,
            new { IntentId = intentId },
            tx,
            cancellationToken: ct))).ToList();

        // 5) Acquire locks on all wallets (sorted order)
        var sortedWalletIds = allocations.Select(a => a.WalletId).OrderBy(id => id).ToList();

        foreach (var walletId in sortedWalletIds)
        {
            var lockAcquired = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
                Sql.AdvisoryLock,
                new { WalletId = walletId },
                tx,
                cancellationToken: ct));

            if (!lockAcquired)
            {
                // Follower: return IN_PROGRESS
                await tx.CommitAsync(ct);

                return new CaptureIntentAtomicResult(
                    Outcome: CaptureIntentOutcome.InProgress,
                    PaymentId: Guid.Empty,
                    IntentId: intentId,
                    OrderId: intent.OrderId,
                    AmountMinor: intent.AmountMinor,
                    Currency: intent.Currency,
                    Allocations: new List<PaymentAllocationOutput>(),
                    CompletedAt: now);
            }
        }

        // 6) Create payment record
        var paymentId = Guid.NewGuid();

        await conn.ExecuteAsync(new CommandDefinition(
            Sql.PaymentInsert,
            new
            {
                PaymentId = paymentId,
                IntentId = intentId,
                OrderId = intent.OrderId,
                Status = (short)PaymentStatus.Completed,
                AmountMinor = intent.AmountMinor,
                Currency = intent.Currency,
                CompletedAt = now,
                MetadataJson = intent.MetadataJson
            },
            tx,
            cancellationToken: ct));

        // 7) For each allocation: insert PAYMENT ledger entry, update balance, create payment_allocation
        var paymentAllocations = new List<PaymentAllocationOutput>();
        var sequence = 0;

        foreach (var allocation in allocations)
        {
            var ledgerEntryId = Guid.NewGuid();
            var operationId = Guid.NewGuid();

            // Insert PAYMENT ledger entry (from pending)
            await conn.ExecuteAsync(new CommandDefinition(
                Sql.LedgerInsertPaymentFromPending,
                new
                {
                    LedgerEntryId = ledgerEntryId,
                    WalletId = allocation.WalletId,
                    OperationId = operationId,
                    OperationType = (short)OperationType.Payment,
                    EntryType = (short)EntryType.Debit,
                    AmountMinor = allocation.AmountMinor,
                    Currency = intent.Currency,
                    EffectiveAt = now,
                    CreatedAt = now,
                    ExternalReference = $"order:{intent.OrderId}|payment:{paymentId}",
                    MetadataJson = intent.MetadataJson
                },
                tx,
                cancellationToken: ct));

            // Update balance: pending -= amount (available unchanged)
            await conn.ExecuteAsync(new CommandDefinition(
                Sql.BalanceUpdateCapture,
                new
                {
                    WalletId = allocation.WalletId,
                    AmountMinor = allocation.AmountMinor,
                    LastLedgerEntryId = ledgerEntryId,
                    UpdatedAt = now
                },
                tx,
                cancellationToken: ct));

            // Insert payment_allocation
            await conn.ExecuteAsync(new CommandDefinition(
                Sql.PaymentAllocationInsert,
                new
                {
                    AllocationId = Guid.NewGuid(),
                    PaymentId = paymentId,
                    WalletId = allocation.WalletId,
                    AmountMinor = allocation.AmountMinor,
                    Sequence = sequence++,
                    LedgerEntryId = ledgerEntryId
                },
                tx,
                cancellationToken: ct));

            paymentAllocations.Add(new PaymentAllocationOutput(
                allocation.WalletId,
                allocation.AmountMinor,
                ledgerEntryId));
        }

        // 8) Update intent status → CAPTURED
        await conn.ExecuteAsync(new CommandDefinition(
            Sql.IntentUpdateCaptured,
            new { IntentId = intentId, CapturedAt = now },
            tx,
            cancellationToken: ct));

        // 9) Mark idempotency COMPLETED
        await conn.ExecuteAsync(new CommandDefinition(
            Sql.IntentOperationIdempotencyComplete,
            new
            {
                IntentId = intentId,
                IdempotencyKey = idempotencyKey,
                OperationType = operationType,
                ResultPaymentId = paymentId,
                CompletedAt = now
            },
            tx,
            cancellationToken: ct));

        await tx.CommitAsync(ct);

        return new CaptureIntentAtomicResult(
            Outcome: CaptureIntentOutcome.Captured,
            PaymentId: paymentId,
            IntentId: intentId,
            OrderId: intent.OrderId,
            AmountMinor: intent.AmountMinor,
            Currency: intent.Currency,
            Allocations: paymentAllocations,
            CompletedAt: now);
    }

    // ================================================================
    // RELEASE INTENT (CANCEL RESERVATION)
    // ================================================================
    public async Task<ReleaseIntentAtomicResult> ExecuteReleaseIntentAsync(
        Guid intentId,
        string idempotencyKey,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var operationType = (short)OperationType.Release;

        // 1) Resolve idempotency
        var existingIdem = await conn.QuerySingleOrDefaultAsync<IntentOperationIdempotencyRow>(
            new CommandDefinition(
                Sql.IntentOperationIdempotencySelect,
                new { IntentId = intentId, IdempotencyKey = idempotencyKey, OperationType = operationType },
                tx,
                cancellationToken: ct));

        if (existingIdem is not null && existingIdem.Status == 2) // Completed
        {
            // Return cached
            var cachedIntentData = await conn.QuerySingleAsync<IntentRow>(new CommandDefinition(
                "select intent_id as IntentId, order_id as OrderId, amount_minor as AmountMinor, currency as Currency, created_at as CreatedAt from wallets.payment_intents where intent_id = @IntentId",
                new { IntentId = intentId },
                tx,
                cancellationToken: ct));

            await tx.CommitAsync(ct);

            return new ReleaseIntentAtomicResult(
                Outcome: ReleaseIntentOutcome.ReleasedCached,
                IntentId: intentId,
                OrderId: cachedIntentData.OrderId,
                ReleasedAt: cachedIntentData.CreatedAt); // or cached released_at if available
        }

        if (existingIdem is null)
        {
            // Insert pending idempotency
            await conn.ExecuteAsync(new CommandDefinition(
                Sql.IntentOperationIdempotencyInsert,
                new
                {
                    IdempotencyId = Guid.NewGuid(),
                    IntentId = intentId,
                    IdempotencyKey = idempotencyKey,
                    OperationType = operationType,
                    Status = (short)1,
                    CreatedAt = now
                },
                tx,
                cancellationToken: ct));
        }

        // 2) Load intent and verify state
        var intent = await conn.QuerySingleOrDefaultAsync<IntentWithStatusRow>(new CommandDefinition(
            Sql.IntentSelectWithStatus,
            new { IntentId = intentId },
            tx,
            cancellationToken: ct));

        if (intent is null)
            throw new PaymentIntentNotFoundException(intentId);

        if ((PaymentIntentStatus)intent.Status == PaymentIntentStatus.Captured)
            throw new PaymentIntentStateViolationException("release", "captured");

        if ((PaymentIntentStatus)intent.Status == PaymentIntentStatus.Released)
        {
            // Already released - cached
            await tx.CommitAsync(ct);

            return new ReleaseIntentAtomicResult(
                Outcome: ReleaseIntentOutcome.ReleasedCached,
                IntentId: intentId,
                OrderId: intent.OrderId,
                ReleasedAt: intent.ReleasedAt ?? now);
        }

        // Load allocations
        var allocations = (await conn.QueryAsync<AllocationRow>(new CommandDefinition(
            Sql.AllocationsSelectByIntent,
            new { IntentId = intentId },
            tx,
            cancellationToken: ct))).ToList();

        // 3) Acquire locks
        var sortedWalletIds = allocations.Select(a => a.WalletId).OrderBy(id => id).ToList();

        foreach (var walletId in sortedWalletIds)
        {
            var lockAcquired = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
                Sql.AdvisoryLock,
                new { WalletId = walletId },
                tx,
                cancellationToken: ct));

            if (!lockAcquired)
            {
                await tx.CommitAsync(ct);

                return new ReleaseIntentAtomicResult(
                    Outcome: ReleaseIntentOutcome.InProgress,
                    IntentId: intentId,
                    OrderId: intent.OrderId,
                    ReleasedAt: now);
            }
        }

        // 4) For each allocation: insert RELEASE ledger, update balance
        var intentData = new IntentRow(intentId, intent.OrderId, intent.AmountMinor, intent.Currency, intent.CreatedAt);

        foreach (var allocation in allocations)
        {
            var ledgerEntryId = Guid.NewGuid();
            var operationId = Guid.NewGuid();

            await conn.ExecuteAsync(new CommandDefinition(
                Sql.LedgerInsertRelease,
                new
                {
                    LedgerEntryId = ledgerEntryId,
                    WalletId = allocation.WalletId,
                    OperationId = operationId,
                    OperationType = (short)OperationType.Release,
                    EntryType = (short)EntryType.ReleaseHold,
                    AmountMinor = allocation.AmountMinor,
                    Currency = intent.Currency,
                    EffectiveAt = now,
                    CreatedAt = now,
                    ExternalReference = $"order:{intent.OrderId}|intent:{intentId}",
                    MetadataJson = intent.MetadataJson
                },
                tx,
                cancellationToken: ct));

            await conn.ExecuteAsync(new CommandDefinition(
                Sql.BalanceUpdateRelease,
                new
                {
                    WalletId = allocation.WalletId,
                    AmountMinor = allocation.AmountMinor,
                    LastLedgerEntryId = ledgerEntryId,
                    UpdatedAt = now
                },
                tx,
                cancellationToken: ct));
        }

        // 5) Update intent status → RELEASED
        await conn.ExecuteAsync(new CommandDefinition(
            Sql.IntentUpdateReleased,
            new { IntentId = intentId, ReleasedAt = now },
            tx,
            cancellationToken: ct));

        // 6) Mark idempotency COMPLETED
        await conn.ExecuteAsync(new CommandDefinition(
            Sql.IntentOperationIdempotencyComplete,
            new
            {
                IntentId = intentId,
                IdempotencyKey = idempotencyKey,
                OperationType = operationType,
                ResultPaymentId = (Guid?)null,
                CompletedAt = now
            },
            tx,
            cancellationToken: ct));

        await tx.CommitAsync(ct);

        return new ReleaseIntentAtomicResult(
            Outcome: ReleaseIntentOutcome.Released,
            IntentId: intentId,
            OrderId: intentData.OrderId,
            ReleasedAt: now);
    }

    // ================================================================
    // REFUND PAYMENT (FULL REFUND)
    // ================================================================
    public async Task<RefundPaymentAtomicResult> ExecuteRefundPaymentAsync(
        Guid paymentId,
        string idempotencyKey,
        string? reason,
        string? metadataJson,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var refundId = Guid.NewGuid();

        // 1) Resolve idempotency (refund-level operation idempotency)
        var existingIdem = await conn.QuerySingleOrDefaultAsync<RefundOperationIdempotencyRow>(
            new CommandDefinition(
                Sql.RefundOperationIdempotencySelect,
                new { PaymentId = paymentId, IdempotencyKey = idempotencyKey },
                tx,
                cancellationToken: ct));

        if (existingIdem is not null)
        {
            // 2) If completed → return cached result
            if (existingIdem.Status == 2) // Completed
            {
                var cachedRefund = await LoadRefundWithAllocations(conn, tx, existingIdem.ResultRefundId!.Value, ct);
                await tx.CommitAsync(ct);

                return new RefundPaymentAtomicResult(
                    Outcome: RefundPaymentOutcome.RefundedCached,
                    RefundId: cachedRefund.RefundId,
                    PaymentId: paymentId,
                    OrderId: cachedRefund.OrderId,
                    AmountMinor: cachedRefund.AmountMinor,
                    Currency: cachedRefund.Currency,
                    Allocations: cachedRefund.Allocations,
                    CompletedAt: cachedRefund.CompletedAt);
            }

            // 3) If pending and not leader → return IN_PROGRESS
            // (try-lock will be attempted below to determine leadership)
        }
        else
        {
            // Insert pending idempotency record
            await conn.ExecuteAsync(new CommandDefinition(
                Sql.RefundOperationIdempotencyInsert,
                new
                {
                    IdempotencyId = Guid.NewGuid(),
                    PaymentId = paymentId,
                    IdempotencyKey = idempotencyKey,
                    Status = (short)1, // Pending
                    CreatedAt = now
                },
                tx,
                cancellationToken: ct));
        }

        // 4) Load payment and verify status is COMPLETED
        var payment = await conn.QuerySingleOrDefaultAsync<PaymentWithStatusRow>(new CommandDefinition(
            Sql.PaymentSelectWithStatus,
            new { PaymentId = paymentId },
            tx,
            cancellationToken: ct));

        if (payment is null)
            throw new PaymentNotFoundException(paymentId);

        if ((PaymentStatus)payment.Status != PaymentStatus.Completed)
            throw new PaymentNotRefundableException(paymentId, "payment not completed");

        // Check if already refunded (by looking for existing refund record)
        var existingRefund = await conn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            Sql.RefundSelectByPayment,
            new { PaymentId = paymentId },
            tx,
            cancellationToken: ct));

        if (existingRefund.HasValue)
        {
            // Payment already refunded with different idempotency key
            throw new PaymentAlreadyRefundedException(paymentId);
        }

        // Load payment allocations (these define how refund will be distributed)
        var allocations = (await conn.QueryAsync<PaymentAllocationRow>(new CommandDefinition(
            Sql.PaymentAllocationsSelectByPayment,
            new { PaymentId = paymentId },
            tx,
            cancellationToken: ct))).ToList();

        // 5) Acquire locks on all wallets (sorted order)
        var sortedWalletIds = allocations.Select(a => a.WalletId).OrderBy(id => id).ToList();

        foreach (var walletId in sortedWalletIds)
        {
            var lockAcquired = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
                Sql.AdvisoryLock,
                new { WalletId = walletId },
                tx,
                cancellationToken: ct));

            if (!lockAcquired)
            {
                // Follower: return IN_PROGRESS
                await tx.CommitAsync(ct);

                return new RefundPaymentAtomicResult(
                    Outcome: RefundPaymentOutcome.InProgress,
                    RefundId: refundId,
                    PaymentId: paymentId,
                    OrderId: payment.OrderId,
                    AmountMinor: payment.AmountMinor,
                    Currency: payment.Currency,
                    Allocations: new List<RefundAllocationOutput>(),
                    CompletedAt: now);
            }
        }

        // 6) Create refund record
        await conn.ExecuteAsync(new CommandDefinition(
            Sql.RefundInsert,
            new
            {
                RefundId = refundId,
                PaymentId = paymentId,
                OrderId = payment.OrderId,
                Status = (short)RefundStatus.Completed,
                AmountMinor = payment.AmountMinor,
                Currency = payment.Currency,
                Reason = reason,
                CreatedAt = now,
                CompletedAt = now,
                MetadataJson = metadataJson
            },
            tx,
            cancellationToken: ct));

        // 7) For each allocation: insert CREDIT ledger entry, update balance, create refund_allocation
        var refundAllocations = new List<RefundAllocationOutput>();
        var sequence = 0;

        foreach (var allocation in allocations)
        {
            var ledgerEntryId = Guid.NewGuid();
            var operationId = Guid.NewGuid();

            // Insert REFUND ledger entry (credit to wallet)
            await conn.ExecuteAsync(new CommandDefinition(
                Sql.LedgerInsertRefund,
                new
                {
                    LedgerEntryId = ledgerEntryId,
                    WalletId = allocation.WalletId,
                    OperationId = operationId,
                    OperationType = (short)OperationType.Refund,
                    EntryType = (short)EntryType.Credit,
                    AmountMinor = allocation.AmountMinor,
                    Currency = payment.Currency,
                    EffectiveAt = now,
                    CreatedAt = now,
                    ExternalReference = $"order:{payment.OrderId}|payment:{paymentId}|refund:{refundId}",
                    MetadataJson = metadataJson
                },
                tx,
                cancellationToken: ct));

            // Update balance: available += amount (refund goes back to available)
            await conn.ExecuteAsync(new CommandDefinition(
                Sql.BalanceUpdateRefund,
                new
                {
                    WalletId = allocation.WalletId,
                    AmountMinor = allocation.AmountMinor,
                    LastLedgerEntryId = ledgerEntryId,
                    UpdatedAt = now
                },
                tx,
                cancellationToken: ct));

            // Insert refund_allocation
            await conn.ExecuteAsync(new CommandDefinition(
                Sql.RefundAllocationInsert,
                new
                {
                    AllocationId = Guid.NewGuid(),
                    RefundId = refundId,
                    WalletId = allocation.WalletId,
                    AmountMinor = allocation.AmountMinor,
                    Sequence = sequence++,
                    LedgerEntryId = ledgerEntryId
                },
                tx,
                cancellationToken: ct));

            refundAllocations.Add(new RefundAllocationOutput(
                allocation.WalletId,
                allocation.AmountMinor,
                ledgerEntryId));
        }

        // 8) Mark idempotency COMPLETED
        await conn.ExecuteAsync(new CommandDefinition(
            Sql.RefundOperationIdempotencyComplete,
            new
            {
                PaymentId = paymentId,
                IdempotencyKey = idempotencyKey,
                ResultRefundId = refundId,
                CompletedAt = now
            },
            tx,
            cancellationToken: ct));

        await tx.CommitAsync(ct);

        return new RefundPaymentAtomicResult(
            Outcome: RefundPaymentOutcome.Refunded,
            RefundId: refundId,
            PaymentId: paymentId,
            OrderId: payment.OrderId,
            AmountMinor: payment.AmountMinor,
            Currency: payment.Currency,
            Allocations: refundAllocations,
            CompletedAt: now);
    }

    // ================================================================
    // HELPER: Load payment with allocations
    // ================================================================
    private static async Task<PaymentWithAllocationsRow> LoadPaymentWithAllocations(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid paymentId,
        CancellationToken ct)
    {
        var payment = await conn.QuerySingleAsync<PaymentRow>(new CommandDefinition(
            Sql.PaymentSelect,
            new { PaymentId = paymentId },
            tx,
            cancellationToken: ct));

        var allocations = await conn.QueryAsync<PaymentAllocationRow>(new CommandDefinition(
            Sql.PaymentAllocationsSelectByPayment,
            new { PaymentId = paymentId },
            tx,
            cancellationToken: ct));

        return new PaymentWithAllocationsRow(
            payment.PaymentId,
            payment.OrderId,
            payment.AmountMinor,
            payment.Currency,
            allocations.Select(a => new PaymentAllocationOutput(a.WalletId, a.AmountMinor, a.LedgerEntryId)).ToList(),
            payment.CompletedAt);
    }

    private static async Task<PaymentWithAllocationsRow> LoadPaymentByIntent(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid intentId,
        CancellationToken ct)
    {
        var payment = await conn.QuerySingleAsync<PaymentRow>(new CommandDefinition(
            Sql.PaymentSelectByIntent,
            new { IntentId = intentId },
            tx,
            cancellationToken: ct));

        return await LoadPaymentWithAllocations(conn, tx, payment.PaymentId, ct);
    }

    // ================================================================
    // HELPER: Load refund with allocations
    // ================================================================
    private static async Task<RefundWithAllocationsRow> LoadRefundWithAllocations(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        Guid refundId,
        CancellationToken ct)
    {
        var refund = await conn.QuerySingleAsync<RefundRow>(new CommandDefinition(
            Sql.RefundSelect,
            new { RefundId = refundId },
            tx,
            cancellationToken: ct));

        var allocations = await conn.QueryAsync<RefundAllocationRow>(new CommandDefinition(
            Sql.RefundAllocationsSelectByRefund,
            new { RefundId = refundId },
            tx,
            cancellationToken: ct));

        return new RefundWithAllocationsRow(
            refund.RefundId,
            refund.OrderId,
            refund.AmountMinor,
            refund.Currency,
            allocations.Select(a => new RefundAllocationOutput(a.WalletId, a.AmountMinor, a.LedgerEntryId)).ToList(),
            refund.CompletedAt);
    }

    // ================================================================
    // RECORD TYPES (Internal DTOs)
    // ================================================================
    private sealed record IntentIdempotencyRow(Guid IntentId);
    private sealed record IntentRow(Guid IntentId, Guid OrderId, long AmountMinor, string Currency, DateTimeOffset CreatedAt);
    private sealed record IntentWithStatusRow(Guid IntentId, Guid OrderId, long AmountMinor, string Currency, short Status, DateTimeOffset CreatedAt, DateTimeOffset? CapturedAt, DateTimeOffset? ReleasedAt, string? MetadataJson);
    private sealed record IntentOperationIdempotencyRow(short Status, Guid? ResultPaymentId);
    private sealed record AllocationRow(Guid WalletId, long AmountMinor);
    private sealed record IntentWithAllocationsRow(Guid IntentId, Guid OrderId, long AmountMinor, string Currency, List<AllocationOutput> Allocations, DateTimeOffset CreatedAt);
    private sealed record WalletRow(Guid WalletId, string Currency, short Status);
    private sealed record BalanceRow(long AvailableMinor, long PendingMinor);
    private sealed record PaymentRow(Guid PaymentId, Guid OrderId, long AmountMinor, string Currency, DateTimeOffset CompletedAt);
    private sealed record PaymentWithStatusRow(Guid PaymentId, Guid OrderId, long AmountMinor, string Currency, short Status, DateTimeOffset CompletedAt);
    private sealed record PaymentAllocationRow(Guid WalletId, long AmountMinor, Guid LedgerEntryId);
    private sealed record PaymentWithAllocationsRow(Guid PaymentId, Guid OrderId, long AmountMinor, string Currency, List<PaymentAllocationOutput> Allocations, DateTimeOffset CompletedAt);
    private sealed record RefundOperationIdempotencyRow(short Status, Guid? ResultRefundId);
    private sealed record RefundRow(Guid RefundId, Guid OrderId, long AmountMinor, string Currency, DateTimeOffset CompletedAt);
    private sealed record RefundAllocationRow(Guid WalletId, long AmountMinor, Guid LedgerEntryId);
    private sealed record RefundWithAllocationsRow(Guid RefundId, Guid OrderId, long AmountMinor, string Currency, List<RefundAllocationOutput> Allocations, DateTimeOffset CompletedAt);

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

        // ================================================================
        // CAPTURE INTENT SQL
        // ================================================================
        public const string IntentSelectWithStatus = @"
select intent_id as IntentId, order_id as OrderId, amount_minor as AmountMinor, currency as Currency,
       status as Status, created_at as CreatedAt, captured_at as CapturedAt, released_at as ReleasedAt,
       metadata as MetadataJson
from wallets.payment_intents
where intent_id = @IntentId;
";

        public const string IntentOperationIdempotencySelect = @"
select status as Status, result_payment_id as ResultPaymentId
from wallets.intent_operation_idempotency
where intent_id = @IntentId and idempotency_key = @IdempotencyKey and operation_type = @OperationType;
";

        public const string IntentOperationIdempotencyInsert = @"
insert into wallets.intent_operation_idempotency
(idempotency_id, intent_id, idempotency_key, operation_type, status, created_at)
values (@IdempotencyId, @IntentId, @IdempotencyKey, @OperationType, @Status, @CreatedAt);
";

        public const string IntentOperationIdempotencyComplete = @"
update wallets.intent_operation_idempotency
set status = 2, result_payment_id = @ResultPaymentId, completed_at = @CompletedAt
where intent_id = @IntentId and idempotency_key = @IdempotencyKey and operation_type = @OperationType;
";

        public const string PaymentInsert = @"
insert into wallets.payments (payment_id, intent_id, order_id, status, amount_minor, currency, completed_at, metadata)
values (@PaymentId, @IntentId, @OrderId, @Status, @AmountMinor, @Currency, @CompletedAt,
        case when @MetadataJson is null then null else cast(@MetadataJson as jsonb) end);
";

        public const string LedgerInsertPaymentFromPending = @"
insert into wallets.ledger_entries
(ledger_entry_id, wallet_id, operation_id, operation_type, entry_type, amount_minor, currency,
 effective_at, created_at, external_reference, metadata)
values
(@LedgerEntryId, @WalletId, @OperationId, @OperationType, @EntryType, @AmountMinor, @Currency,
 @EffectiveAt, @CreatedAt, @ExternalReference,
 case when @MetadataJson is null then null else cast(@MetadataJson as jsonb) end);
";

        public const string BalanceUpdateCapture = @"
update wallets.wallet_balances
set
  pending_minor = pending_minor - @AmountMinor,
  last_ledger_entry_id = @LastLedgerEntryId,
  updated_at = @UpdatedAt,
  version = version + 1
where wallet_id = @WalletId;
";

        public const string PaymentAllocationInsert = @"
insert into wallets.payment_allocations (allocation_id, payment_id, wallet_id, amount_minor, sequence, ledger_entry_id)
values (@AllocationId, @PaymentId, @WalletId, @AmountMinor, @Sequence, @LedgerEntryId);
";

        public const string IntentUpdateCaptured = @"
update wallets.payment_intents
set status = 2, captured_at = @CapturedAt
where intent_id = @IntentId;
";

        public const string PaymentSelect = @"
select payment_id as PaymentId, order_id as OrderId, amount_minor as AmountMinor,
       currency as Currency, completed_at as CompletedAt
from wallets.payments
where payment_id = @PaymentId;
";

        public const string PaymentSelectByIntent = @"
select payment_id as PaymentId, order_id as OrderId, amount_minor as AmountMinor,
       currency as Currency, completed_at as CompletedAt
from wallets.payments
where intent_id = @IntentId;
";

        public const string PaymentAllocationsSelectByPayment = @"
select wallet_id as WalletId, amount_minor as AmountMinor, ledger_entry_id as LedgerEntryId
from wallets.payment_allocations
where payment_id = @PaymentId
order by sequence;
";

        // ================================================================
        // RELEASE INTENT SQL
        // ================================================================
        public const string LedgerInsertRelease = @"
insert into wallets.ledger_entries
(ledger_entry_id, wallet_id, operation_id, operation_type, entry_type, amount_minor, currency,
 effective_at, created_at, external_reference, metadata)
values
(@LedgerEntryId, @WalletId, @OperationId, @OperationType, @EntryType, @AmountMinor, @Currency,
 @EffectiveAt, @CreatedAt, @ExternalReference,
 case when @MetadataJson is null then null else cast(@MetadataJson as jsonb) end);
";

        public const string BalanceUpdateRelease = @"
update wallets.wallet_balances
set
  available_minor = available_minor + @AmountMinor,
  pending_minor = pending_minor - @AmountMinor,
  last_ledger_entry_id = @LastLedgerEntryId,
  updated_at = @UpdatedAt,
  version = version + 1
where wallet_id = @WalletId;
";

        public const string IntentUpdateReleased = @"
update wallets.payment_intents
set status = 3, released_at = @ReleasedAt
where intent_id = @IntentId;
";

        // ================================================================
        // REFUND SQL
        // ================================================================
        public const string RefundOperationIdempotencySelect = @"
select status as Status, result_refund_id as ResultRefundId
from wallets.refund_operation_idempotency
where payment_id = @PaymentId and idempotency_key = @IdempotencyKey;
";

        public const string RefundOperationIdempotencyInsert = @"
insert into wallets.refund_operation_idempotency
(idempotency_id, payment_id, idempotency_key, status, created_at)
values (@IdempotencyId, @PaymentId, @IdempotencyKey, @Status, @CreatedAt);
";

        public const string RefundOperationIdempotencyComplete = @"
update wallets.refund_operation_idempotency
set status = 2, result_refund_id = @ResultRefundId, completed_at = @CompletedAt
where payment_id = @PaymentId and idempotency_key = @IdempotencyKey;
";

        public const string PaymentSelectWithStatus = @"
select payment_id as PaymentId, order_id as OrderId, amount_minor as AmountMinor,
       currency as Currency, status as Status, completed_at as CompletedAt
from wallets.payments
where payment_id = @PaymentId;
";

        public const string RefundSelectByPayment = @"
select refund_id as RefundId
from wallets.refunds
where payment_id = @PaymentId;
";

        public const string RefundInsert = @"
insert into wallets.refunds (refund_id, payment_id, order_id, status, amount_minor, currency, reason, created_at, completed_at, metadata)
values (@RefundId, @PaymentId, @OrderId, @Status, @AmountMinor, @Currency, @Reason, @CreatedAt, @CompletedAt,
        case when @MetadataJson is null then null else cast(@MetadataJson as jsonb) end);
";

        public const string LedgerInsertRefund = @"
insert into wallets.ledger_entries
(ledger_entry_id, wallet_id, operation_id, operation_type, entry_type, amount_minor, currency,
 effective_at, created_at, external_reference, metadata)
values
(@LedgerEntryId, @WalletId, @OperationId, @OperationType, @EntryType, @AmountMinor, @Currency,
 @EffectiveAt, @CreatedAt, @ExternalReference,
 case when @MetadataJson is null then null else cast(@MetadataJson as jsonb) end);
";

        public const string BalanceUpdateRefund = @"
update wallets.wallet_balances
set
  available_minor = available_minor + @AmountMinor,
  last_ledger_entry_id = @LastLedgerEntryId,
  updated_at = @UpdatedAt,
  version = version + 1
where wallet_id = @WalletId;
";

        public const string RefundAllocationInsert = @"
insert into wallets.refund_allocations (allocation_id, refund_id, wallet_id, amount_minor, sequence, ledger_entry_id)
values (@AllocationId, @RefundId, @WalletId, @AmountMinor, @Sequence, @LedgerEntryId);
";

        public const string RefundSelect = @"
select refund_id as RefundId, order_id as OrderId, amount_minor as AmountMinor,
       currency as Currency, completed_at as CompletedAt
from wallets.refunds
where refund_id = @RefundId;
";

        public const string RefundAllocationsSelectByRefund = @"
select wallet_id as WalletId, amount_minor as AmountMinor, ledger_entry_id as LedgerEntryId
from wallets.refund_allocations
where refund_id = @RefundId
order by sequence;
";
    }
}
