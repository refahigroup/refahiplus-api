using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Services;

/// <summary>
/// Application Service: Refund Payment (Full Refund) use case.
/// 
/// Responsibilities:
/// - Orchestrate call to Infrastructure
/// - Interpret outcome and build response
/// - Handle state machine violations (via Infrastructure exceptions)
/// </summary>
public sealed class RefundPaymentApplicationService
{
    private readonly IPaymentAtomicWriter _atomicWriter;

    public RefundPaymentApplicationService(IPaymentAtomicWriter atomicWriter)
    {
        _atomicWriter = atomicWriter;
    }

    public async Task<CommandResponse<RefundPaymentResponse>> RefundPaymentAsync(
        RefundPaymentCommand command,
        CancellationToken ct)
    {
        // Delegate atomic execution to Infrastructure
        var atomicResult = await _atomicWriter.ExecuteRefundPaymentAsync(
            paymentId: command.PaymentId,
            idempotencyKey: command.IdempotencyKey,
            reason: command.Reason,
            metadataJson: command.MetadataJson,
            ct: ct);

        // Interpret outcome and build response
        return atomicResult.Outcome switch
        {
            RefundPaymentOutcome.Refunded or RefundPaymentOutcome.RefundedCached => BuildCompletedResponse(atomicResult),
            RefundPaymentOutcome.InProgress => BuildInProgressResponse(),
            _ => throw new InvalidOperationException($"Unknown outcome: {atomicResult.Outcome}")
        };
    }

    private static CommandResponse<RefundPaymentResponse> BuildCompletedResponse(
        RefundPaymentAtomicResult atomicResult)
    {
        var allocations = atomicResult.Allocations
            .Select(a => new RefundAllocationResponse(a.WalletId, a.AmountMinor, a.LedgerEntryId))
            .ToList();

        var response = new RefundPaymentResponse(
            RefundId: atomicResult.RefundId,
            PaymentId: atomicResult.PaymentId,
            OrderId: atomicResult.OrderId,
            Status: "Completed",
            AmountMinor: atomicResult.AmountMinor,
            Currency: atomicResult.Currency,
            Allocations: allocations,
            CompletedAt: atomicResult.CompletedAt);

        return new CommandResponse<RefundPaymentResponse>(CommandStatus.Completed, response);
    }

    private static CommandResponse<RefundPaymentResponse> BuildInProgressResponse()
    {
        return new CommandResponse<RefundPaymentResponse>(CommandStatus.InProgress, null);
    }
}
