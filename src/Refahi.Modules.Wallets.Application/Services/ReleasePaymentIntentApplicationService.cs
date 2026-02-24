using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Services;

/// <summary>
/// Application Service: Release Payment Intent (Cancel Reservation) use case.
/// 
/// Responsibilities:
/// - Orchestrate call to Infrastructure
/// - Interpret outcome and build response
/// - Handle state machine violations (via Infrastructure exceptions)
/// </summary>
public sealed class ReleasePaymentIntentApplicationService
{
    private readonly IPaymentAtomicWriter _atomicWriter;

    public ReleasePaymentIntentApplicationService(IPaymentAtomicWriter atomicWriter)
    {
        _atomicWriter = atomicWriter;
    }

    public async Task<CommandResponse<ReleasePaymentIntentResponse>> ReleaseIntentAsync(
        ReleasePaymentIntentCommand command,
        CancellationToken ct)
    {
        // Delegate atomic execution to Infrastructure
        var atomicResult = await _atomicWriter.ExecuteReleaseIntentAsync(
            intentId: command.IntentId,
            idempotencyKey: command.IdempotencyKey,
            ct: ct);

        // Interpret outcome and build response
        return atomicResult.Outcome switch
        {
            ReleaseIntentOutcome.Released or ReleaseIntentOutcome.ReleasedCached => BuildCompletedResponse(atomicResult),
            ReleaseIntentOutcome.InProgress => BuildInProgressResponse(),
            _ => throw new InvalidOperationException($"Unknown outcome: {atomicResult.Outcome}")
        };
    }

    private static CommandResponse<ReleasePaymentIntentResponse> BuildCompletedResponse(
        ReleaseIntentAtomicResult atomicResult)
    {
        var response = new ReleasePaymentIntentResponse(
            IntentId: atomicResult.IntentId,
            OrderId: atomicResult.OrderId,
            Status: "Released",
            ReleasedAt: atomicResult.ReleasedAt);

        return new CommandResponse<ReleasePaymentIntentResponse>(CommandStatus.Completed, response);
    }

    private static CommandResponse<ReleasePaymentIntentResponse> BuildInProgressResponse()
    {
        return new CommandResponse<ReleasePaymentIntentResponse>(CommandStatus.InProgress, null);
    }
}
