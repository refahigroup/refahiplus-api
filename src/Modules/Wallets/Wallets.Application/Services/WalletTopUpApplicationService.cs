using System;
using System.Threading;
using System.Threading.Tasks;
using Wallets.Application.Contracts;
using Wallets.Application.Contracts.Features.TopUp;
using Wallets.Application.Contracts.Infrastructure;
using Wallets.Application.Contracts.Usecases;
using Wallets.Domain.Aggregates;

namespace Wallets.Application.Services;

/// <summary>
/// Application Service: Wallet TopUp use case orchestration.
/// 
/// Responsibilities:
/// - Input normalization (currency)
/// - Orchestrate call to Infrastructure atomic writer
/// - Interpret atomic execution outcome
/// - Build business response (CommandResponse)
/// 
/// Does NOT:
/// - Execute SQL
/// - Manage transactions
/// - Handle locks
/// 
/// This is the correct layer for business orchestration logic.
/// </summary>
public sealed class WalletTopUpApplicationService : IWalletTopUpUsecase
{
    private readonly IWalletAtomicWriter _atomicWriter;

    public WalletTopUpApplicationService(IWalletAtomicWriter atomicWriter)
    {
        _atomicWriter = atomicWriter;
    }

    public async Task<CommandResponse<TopUpWalletResponse>> TopUpAsync(
        TopUpWalletCommand command,
        CancellationToken ct)
    {
        // 1) Business rule: Normalize currency through domain
        var currency = Wallet.NormalizeCurrency(command.Currency);

        // 2) Delegate atomic execution to Infrastructure
        var atomicResult = await _atomicWriter.ExecuteTopUpAsync(
            walletId: command.WalletId,
            idempotencyKey: command.IdempotencyKey,
            amountMinor: command.AmountMinor,
            currency: currency,
            externalReference: command.ExternalReference,
            metadataJson: command.MetadataJson,
            ct: ct);

        // 3) Interpret outcome and build business response
        return atomicResult.Outcome switch
        {
            TopUpOutcome.Completed or TopUpOutcome.CompletedCached => BuildCompletedResponse(atomicResult, command, currency),
            TopUpOutcome.InProgress => BuildInProgressResponse(),
            _ => throw new InvalidOperationException($"Unknown outcome: {atomicResult.Outcome}")
        };
    }

    private static CommandResponse<TopUpWalletResponse> BuildCompletedResponse(
        WalletTopUpAtomicResult atomicResult,
        TopUpWalletCommand command,
        string currency)
    {
        var response = new TopUpWalletResponse(
            WalletId: command.WalletId,
            OperationId: atomicResult.OperationId,
            LedgerEntryId: atomicResult.LedgerEntryId ?? Guid.Empty,
            AmountMinor: command.AmountMinor,
            Currency: currency,
            AvailableBalanceMinor: atomicResult.AvailableBalanceMinor,
            CreatedAt: atomicResult.CompletedAt);

        return new CommandResponse<TopUpWalletResponse>(CommandStatus.Completed, response);
    }

    private static CommandResponse<TopUpWalletResponse> BuildInProgressResponse()
    {
        return new CommandResponse<TopUpWalletResponse>(CommandStatus.InProgress, null);
    }
}
