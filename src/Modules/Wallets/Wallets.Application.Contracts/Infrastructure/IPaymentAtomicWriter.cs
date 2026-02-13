using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wallets.Application.Contracts.Infrastructure;

/// <summary>
/// Infrastructure contract for atomic payment intent operations.
/// 
/// Responsibilities:
/// - Execute SQL operations within transactions
/// - Manage multi-wallet advisory locks (deadlock avoidance)
/// - Handle idempotency
/// - Return structured outcomes
/// 
/// NOT responsible for:
/// - Business logic interpretation
/// - Response building
/// - Validation (Application layer responsibility)
/// </summary>
public interface IPaymentAtomicWriter
{
    /// <summary>
    /// Atomically create a payment intent (reserve).
    /// 
    /// Multi-wallet operation:
    /// - Locks wallets in stable order (sorted by wallet_id) to prevent deadlocks
    /// - Validates wallet existence/currency/status for each allocation
    /// - Inserts HOLD ledger entries (reduces available balance)
    /// - Creates intent + allocations records
    /// - Idempotent per (order_id, idempotency_key)
    /// </summary>
    Task<CreateIntentAtomicResult> ExecuteCreateIntentAsync(
        Guid orderId,
        string idempotencyKey,
        long totalAmountMinor,
        string currency,
        List<AllocationInput> allocations,
        string? metadataJson,
        CancellationToken ct);

    /// <summary>
    /// Atomically capture a payment intent (finalize payment).
    /// 
    /// State machine:
    /// - Reserved → Captured (success)
    /// - Captured → Captured (idempotent)
    /// - Released → InvalidTransition exception
    /// 
    /// Multi-wallet operation:
    /// - Creates DEBIT ledger entries (Payment)
    /// - Creates RELEASE ledger entries (to release the HOLD)
    /// - Creates payment record + allocations
    /// - Updates intent status to Captured
    /// - Idempotent per (intent_id, idempotency_key)
    /// </summary>
    Task<CaptureIntentAtomicResult> ExecuteCaptureIntentAsync(
        Guid intentId,
        string idempotencyKey,
        CancellationToken ct);

    /// <summary>
    /// Atomically release a payment intent (cancel reservation).
    /// 
    /// State machine:
    /// - Reserved → Released (success)
    /// - Released → Released (idempotent)
    /// - Captured → InvalidTransition exception
    /// 
    /// Multi-wallet operation:
    /// - Creates RELEASE ledger entries (to undo HOLD)
    /// - Updates intent status to Released
    /// - Idempotent per (intent_id, idempotency_key)
    /// </summary>
    Task<ReleaseIntentAtomicResult> ExecuteReleaseIntentAsync(
        Guid intentId,
        string idempotencyKey,
        CancellationToken ct);
}

/// <summary>
/// Input for allocation (wallet + amount).
/// </summary>
public sealed record AllocationInput(
    Guid WalletId,
    long AmountMinor);

/// <summary>
/// Result of create intent execution.
/// </summary>
public sealed record CreateIntentAtomicResult(
    CreateIntentOutcome Outcome,
    Guid IntentId,
    Guid OrderId,
    long AmountMinor,
    string Currency,
    List<AllocationOutput> Allocations,
    DateTimeOffset CreatedAt);

/// <summary>
/// Result of capture intent execution.
/// </summary>
public sealed record CaptureIntentAtomicResult(
    CaptureIntentOutcome Outcome,
    Guid PaymentId,
    Guid IntentId,
    Guid OrderId,
    long AmountMinor,
    string Currency,
    List<PaymentAllocationOutput> Allocations,
    DateTimeOffset CompletedAt);

/// <summary>
/// Result of release intent execution.
/// </summary>
public sealed record ReleaseIntentAtomicResult(
    ReleaseIntentOutcome Outcome,
    Guid IntentId,
    Guid OrderId,
    DateTimeOffset ReleasedAt);

/// <summary>
/// Output for allocation info.
/// </summary>
public sealed record AllocationOutput(
    Guid WalletId,
    long AmountMinor);

/// <summary>
/// Output for payment allocation (includes ledger entry).
/// </summary>
public sealed record PaymentAllocationOutput(
    Guid WalletId,
    long AmountMinor,
    Guid LedgerEntryId);

/// <summary>
/// Outcome of create intent execution.
/// </summary>
public enum CreateIntentOutcome
{
    /// <summary>
    /// New intent created successfully.
    /// </summary>
    Created = 1,

    /// <summary>
    /// Idempotent retry - intent already exists.
    /// </summary>
    CreatedCached = 2,

    /// <summary>
    /// Concurrent request detected.
    /// </summary>
    InProgress = 3
}

/// <summary>
/// Outcome of capture intent execution.
/// </summary>
public enum CaptureIntentOutcome
{
    /// <summary>
    /// Intent captured successfully.
    /// </summary>
    Captured = 1,

    /// <summary>
    /// Idempotent retry - already captured.
    /// </summary>
    CapturedCached = 2,

    /// <summary>
    /// Concurrent request detected.
    /// </summary>
    InProgress = 3
}

/// <summary>
/// Outcome of release intent execution.
/// </summary>
public enum ReleaseIntentOutcome
{
    /// <summary>
    /// Intent released successfully.
    /// </summary>
    Released = 1,

    /// <summary>
    /// Idempotent retry - already released.
    /// </summary>
    ReleasedCached = 2,

    /// <summary>
    /// Concurrent request detected.
    /// </summary>
    InProgress = 3
}
