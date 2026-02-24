using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.CapturePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Services;

/// <summary>
/// Application Service: Capture Payment Intent (Finalize Payment) use case.
/// 
/// Responsibilities:
/// - Orchestrate call to Infrastructure
/// - Interpret outcome and build response
/// - Handle state machine violations (via Infrastructure exceptions)
/// </summary>
public sealed class CapturePaymentIntentApplicationService
{
    private readonly IPaymentAtomicWriter _atomicWriter;

    public CapturePaymentIntentApplicationService(IPaymentAtomicWriter atomicWriter)
    {
        _atomicWriter = atomicWriter;
    }

    public async Task<CommandResponse<CapturePaymentIntentResponse>> CaptureIntentAsync(
        CapturePaymentIntentCommand command,
        CancellationToken ct)
    {
        // Delegate atomic execution to Infrastructure
        var atomicResult = await _atomicWriter.ExecuteCaptureIntentAsync(
            intentId: command.IntentId,
            idempotencyKey: command.IdempotencyKey,
            ct: ct);

        // Interpret outcome and build response
        return atomicResult.Outcome switch
        {
            CaptureIntentOutcome.Captured or CaptureIntentOutcome.CapturedCached => BuildCompletedResponse(atomicResult),
            CaptureIntentOutcome.InProgress => BuildInProgressResponse(),
            _ => throw new InvalidOperationException($"Unknown outcome: {atomicResult.Outcome}")
        };
    }

    private static CommandResponse<CapturePaymentIntentResponse> BuildCompletedResponse(
        CaptureIntentAtomicResult atomicResult)
    {
        var allocations = atomicResult.Allocations
            .Select(a => new PaymentAllocationResponse(a.WalletId, a.AmountMinor, a.LedgerEntryId))
            .ToList();

        var response = new CapturePaymentIntentResponse(
            PaymentId: atomicResult.PaymentId,
            IntentId: atomicResult.IntentId,
            OrderId: atomicResult.OrderId,
            AmountMinor: atomicResult.AmountMinor,
            Currency: atomicResult.Currency,
            Status: "Completed",
            Allocations: allocations,
            CompletedAt: atomicResult.CompletedAt);

        return new CommandResponse<CapturePaymentIntentResponse>(CommandStatus.Completed, response);
    }

    private static CommandResponse<CapturePaymentIntentResponse> BuildInProgressResponse()
    {
        return new CommandResponse<CapturePaymentIntentResponse>(CommandStatus.InProgress, null);
    }
}
