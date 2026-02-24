using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Contracts.Infrastructure;

/// <summary>
/// Infrastructure contract for atomic wallet write operations.
/// 
/// Responsibilities:
/// - Execute SQL operations within a transaction
/// - Manage advisory locks and idempotency
/// - Return structured outcome (NO business decision about WHAT to return to user)
/// 
/// NOT responsible for:
/// - Business logic interpretation
/// - Response building
/// - Currency normalization
/// </summary>
public interface IWalletAtomicWriter
{
    /// <summary>
    /// Atomically execute a TopUp operation.
    /// 
    /// This method:
    /// - Opens DB connection and transaction
    /// - Acquires advisory lock
    /// - Handles idempotency (insert/check/complete)
    /// - Validates wallet constraints (existence, currency, status)
    /// - Inserts ledger entry
    /// - Updates balance projection
    /// - Commits transaction
    /// 
    /// Returns structured outcome for Application layer to interpret.
    /// Throws domain exceptions for business rule violations.
    /// </summary>
    Task<WalletTopUpAtomicResult> ExecuteTopUpAsync(
        Guid walletId,
        string idempotencyKey,
        long amountMinor,
        string currency,
        string? externalReference,
        string? metadataJson,
        CancellationToken ct);
}

/// <summary>
/// Result of atomic TopUp execution.
/// Infrastructure returns this; Application interprets.
/// </summary>
public sealed record WalletTopUpAtomicResult(
    TopUpOutcome Outcome,
    Guid OperationId,
    Guid? LedgerEntryId,
    long AvailableBalanceMinor,
    DateTimeOffset CompletedAt);

/// <summary>
/// Outcome of TopUp execution.
/// Infrastructure sets this; Application decides what to return to user.
/// </summary>
public enum TopUpOutcome
{
    /// <summary>
    /// New operation executed successfully.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// Idempotent retry - operation already completed, returning cached result.
    /// </summary>
    CompletedCached = 2,

    /// <summary>
    /// Concurrent request detected - operation is pending in another transaction.
    /// </summary>
    InProgress = 3
}
