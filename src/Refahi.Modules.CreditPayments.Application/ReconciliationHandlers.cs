using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Application.Contracts.IntegrationEvents;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.PaymentGateway.Application.Contracts.Funding;
using Refahi.Modules.Wallets.Application.Contracts;
using WalletRefundQuery = Refahi.Modules.Wallets.Application.Contracts.Queries.GetRefundQuery;

namespace Refahi.Modules.CreditPayments.Application;

public sealed class ReconciliationHandlers(IReconciliationAdminReader reader, IReconciliationRepository repository,
    IMediator mediator, IWalletFundingService walletFunding,
    IOptions<ReconciliationOptions> configured, TimeProvider clock) :
    IRequestHandler<StartReconciliationRunCommand, ReconciliationRunDto?>,
    IRequestHandler<ExecuteReconciliationRunCommand, ReconciliationRunDto?>,
    IRequestHandler<SearchReconciliationRunsQuery, PagedAdminResult<ReconciliationRunDto>>,
    IRequestHandler<GetReconciliationRunQuery, ReconciliationRunDto?>,
    IRequestHandler<SearchReconciliationFindingsQuery, PagedAdminResult<ReconciliationFindingDto>>,
    IRequestHandler<GetReconciliationFindingQuery, ReconciliationFindingDto?>,
    IRequestHandler<AcknowledgeReconciliationFindingCommand, bool>
{
    public Task<PagedAdminResult<ReconciliationRunDto>> Handle(SearchReconciliationRunsQuery request, CancellationToken ct) => reader.SearchRunsAsync(request, ct);
    public Task<ReconciliationRunDto?> Handle(GetReconciliationRunQuery request, CancellationToken ct) => reader.GetRunAsync(request.RunId, ct);
    public Task<PagedAdminResult<ReconciliationFindingDto>> Handle(SearchReconciliationFindingsQuery request, CancellationToken ct) => reader.SearchFindingsAsync(request, ct);
    public Task<ReconciliationFindingDto?> Handle(GetReconciliationFindingQuery request, CancellationToken ct) => reader.GetFindingAsync(request.FindingId, ct);

    public async Task<bool> Handle(AcknowledgeReconciliationFindingCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ActorId)) throw new CreditPaymentsApplicationException("admin_actor_required", "شناسه ادمین الزامی است", 401);
        if (request.Note?.Length > 500) throw new CreditPaymentsApplicationException("acknowledgement_note_invalid", "یادداشت حداکثر ۵۰۰ نویسه است");
        return await repository.AcknowledgeFindingAsync(request.FindingId, request.ActorId, request.Note, clock.GetUtcNow(), ct);
    }

    public Task<ReconciliationRunDto?> Handle(StartReconciliationRunCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<ReconciliationKind>(request.Kind, true, out var kind))
            throw new CreditPaymentsApplicationException("reconciliation_kind_invalid", "نوع reconciliation نامعتبر است");
        ValidateRange(request.From, request.To);
        return mediator.Send(new ExecuteReconciliationRunCommand((short)kind, request.From, request.To,
            request.DryRun, request.WorkerInstanceId ?? $"admin-{Environment.MachineName}"), ct);
    }

    public async Task<ReconciliationRunDto?> Handle(ExecuteReconciliationRunCommand request, CancellationToken ct)
    {
        if (!Enum.IsDefined(typeof(ReconciliationKind), request.Kind))
            throw new CreditPaymentsApplicationException("reconciliation_kind_invalid", "نوع reconciliation نامعتبر است");
        ValidateRange(request.From, request.To);
        var kind = (ReconciliationKind)request.Kind; var now = clock.GetUtcNow(); var options = configured.Value;
        var run = await repository.TryStartRunAsync(kind, request.WorkerInstanceId, request.From,
            request.To, request.DryRun, now.AddSeconds(options.LeaseSeconds), now, ct);
        if (run is null) return null;
        var from = request.From ?? now.AddHours(-options.LookbackHours);
        var to = request.To ?? now;
        var scanned = 0; var findings = 0; var remediations = 0; var errors = 0; string? cursor = null;
        try
        {
            switch (kind)
            {
                case ReconciliationKind.Funding:
                    (scanned, findings, remediations, errors, cursor) = await Funding(run.Id, from, to, request.DryRun, ct); break;
                case ReconciliationKind.PaymentExecution:
                    (scanned, findings, remediations, errors, cursor) = await Payments(run.Id, from, to, request.DryRun, ct); break;
                case ReconciliationKind.Refund:
                    (scanned, findings, remediations, errors, cursor) = await Refunds(run.Id, from, to, request.DryRun, ct); break;
                case ReconciliationKind.InternalIntegrity:
                    var internalAudit = await repository.AuditInternalIntegrityAsync(run.Id, from, to, options.BatchSize, now, ct);
                    scanned = internalAudit.Scanned; findings = internalAudit.Findings; break;
                case ReconciliationKind.WebhookIntegrity:
                    if (!request.DryRun && options.AutoRemediationEnabled)
                    {
                        remediations = await mediator.Send(new ReconcileMissingWebhookEventsCommand(options.BatchSize), ct);
                        remediations += await mediator.Send(new ReconcileMissingCreditPaymentIntegrationEventsCommand(options.BatchSize), ct);
                    }
                    var webhookAudit = await repository.AuditWebhookIntegrityAsync(run.Id, from, to, options.BatchSize,
                        TimeSpan.FromMinutes(options.DeadLetterStaleAfterMinutes), now, ct);
                    scanned = webhookAudit.Scanned; findings = webhookAudit.Findings; break;
                case ReconciliationKind.RiskIntegrity:
                    var risk = await repository.AuditRiskIntegrityAsync(run.Id, options.BatchSize,
                        options.AutoRemediationEnabled && !request.DryRun, now, ct);
                    scanned = risk.Scanned; findings = risk.Findings; remediations = risk.Repairs; break;
            }
            await repository.CompleteRunAsync(run.Id, scanned, findings, remediations, errors, cursor, clock.GetUtcNow(), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await repository.FailRunAsync(run.Id, scanned, findings, remediations, errors + 1,
                "reconciliation_canceled", clock.GetUtcNow(), CancellationToken.None); throw;
        }
        catch (Exception)
        {
            await repository.FailRunAsync(run.Id, scanned, findings, remediations, errors + 1,
                "reconciliation_run_failed", clock.GetUtcNow(), CancellationToken.None); throw;
        }
        return await reader.GetRunAsync(run.Id, ct);
    }

    private async Task<(int Scanned, int Findings, int Remediations, int Errors, string? Cursor)> Funding(
        Guid runId, DateTimeOffset from, DateTimeOffset to, bool dryRun, CancellationToken ct)
    {
        var batch = await repository.GetFundingCandidatesAsync(from, to, configured.Value.BatchSize, ct); var rows = batch.Items;
        var findings = 0; var remediations = 0; var errors = 0;
        foreach (var row in rows)
        {
            WalletFundingStatus? external;
            try
            {
                external = row.WalletFundingSessionId is { } id
                    ? await walletFunding.GetStatusAsync(row.CustomerId, id, ct)
                    : await walletFunding.GetStatusByClientReferenceAsync(row.CustomerId, row.ClientReference, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { errors++; continue; }
            var obs = new List<ReconciliationObservation>();
            var stale = clock.GetUtcNow() - row.UpdatedAt >= TimeSpan.FromMinutes(configured.Value.FundingStaleAfterMinutes);
            if (external is null)
            {
                if (stale && row.Status is FundingContinuationStatus.Created or FundingContinuationStatus.RedirectReady or FundingContinuationStatus.PendingRecovery)
                    obs.Add(Obs(ReconciliationFindingCodes.FundingStatusStale, ReconciliationSeverity.Warning,
                        new { cpgStatus = row.Status.ToString(), lastUpdatedAt = row.UpdatedAt }, "RecoverFundingContinuationCommand"));
            }
            else
            {
                if (row.WalletFundingSessionId is { } expectedSession && external.FundingSessionId != expectedSession)
                    obs.Add(Obs(ReconciliationFindingCodes.FundingSessionMismatch, ReconciliationSeverity.Critical, new { expectedSession, observedSession = external.FundingSessionId }));
                if (external.UserId != Guid.Empty && external.UserId != row.CustomerId)
                    obs.Add(Obs(ReconciliationFindingCodes.FundingCustomerMismatch, ReconciliationSeverity.Critical, new { expectedCustomerId = row.CustomerId, observedCustomerId = external.UserId }));
                if (external.WalletId != row.PersonalWalletId)
                    obs.Add(Obs(ReconciliationFindingCodes.FundingWalletMismatch, ReconciliationSeverity.Critical, new { expectedWalletId = row.PersonalWalletId, observedWalletId = external.WalletId }));
                if (external.AmountMinor != row.AmountMinor || !string.Equals(external.Currency, row.Currency, StringComparison.Ordinal))
                    obs.Add(Obs(ReconciliationFindingCodes.FundingAmountMismatch, ReconciliationSeverity.Critical, new { expectedAmountMinor = row.AmountMinor, observedAmountMinor = external.AmountMinor, expectedCurrency = row.Currency, observedCurrency = external.Currency }));
                if (!string.IsNullOrEmpty(external.ClientReference) && !string.Equals(external.ClientReference, row.ClientReference, StringComparison.Ordinal))
                    obs.Add(Obs(ReconciliationFindingCodes.FundingClientReferenceMismatch, ReconciliationSeverity.Critical, new { expectedClientReference = row.ClientReference, observedClientReference = external.ClientReference }));
                var identityClean = obs.All(x => x.Severity != ReconciliationSeverity.Critical);
                if (identityClean && stale && row.Status == FundingContinuationStatus.PendingRecovery &&
                    string.Equals(external.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
                    obs.Add(Obs(ReconciliationFindingCodes.FundingStatusStale, ReconciliationSeverity.Warning,
                        new { cpgStatus = row.Status.ToString(), externalStatus = external.Status }, "RecoverFundingContinuationCommand"));
            }
            var saved = await repository.ReconcileFindingsAsync(runId, ReconciliationKind.Funding,
                "FundingContinuation", row.ContinuationId, row.Version, obs,
                ReconciliationResolutionCategory.ExternalConvergence, clock.GetUtcNow(), ct);
            if (!saved.SnapshotCurrent) continue; findings += obs.Count;
            if (!dryRun) remediations += await Remediate(saved, obs, async () =>
                await mediator.Send(new RecoverFundingContinuationCommand(row.ContinuationId), ct), ct);
        }
        return (rows.Count, findings, remediations, errors, batch.Cursor);
    }

    private async Task<(int Scanned, int Findings, int Remediations, int Errors, string? Cursor)> Payments(
        Guid runId, DateTimeOffset from, DateTimeOffset to, bool dryRun, CancellationToken ct)
    {
        var batch = await repository.GetPaymentCandidatesAsync(from, to, configured.Value.BatchSize, ct); var rows = batch.Items;
        var findings = 0; var remediations = 0; var errors = 0;
        foreach (var row in rows)
        {
            var obs = new List<ReconciliationObservation>();
            if (row.OrderId is null || row.CustomerId is null)
                obs.Add(Obs(ReconciliationFindingCodes.PaymentOrderMissing, ReconciliationSeverity.Critical,
                    new { cpgStatus = row.Status.ToString(), hasOrderId = row.OrderId is not null, hasCustomerId = row.CustomerId is not null }));
            else
            {
                OrderPaymentExecutionStatusDto? external;
                try { external = await mediator.Send(new GetOrderPaymentExecutionStatusQuery(row.OrderId.Value, row.CustomerId.Value), ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch { errors++; continue; }
                if (external is null)
                    obs.Add(Obs(ReconciliationFindingCodes.PaymentOrderBindingMismatch, ReconciliationSeverity.Critical,
                        new { orderId = row.OrderId, customerId = row.CustomerId, reason = "not_found_or_not_owned" }));
                else
                {
                    if (external.UserId != row.CustomerId || external.AmountMinor != row.AmountMinor || external.Currency != "IRR" ||
                        external.SourceModule != "CreditPayments" || external.SourceReferenceId != row.PaymentId)
                        obs.Add(Obs(ReconciliationFindingCodes.PaymentOrderBindingMismatch, ReconciliationSeverity.Critical,
                            new { external.UserId, external.AmountMinor, external.Currency, external.SourceModule, external.SourceReferenceId }));
                    var captured = string.Equals(external.ExecutionStatus, "Succeeded", StringComparison.OrdinalIgnoreCase);
                    if (row.Status is GatewayPaymentStatus.Canceled or GatewayPaymentStatus.Expired && captured)
                        obs.Add(Obs(ReconciliationFindingCodes.TerminalStateCaptureConflict, ReconciliationSeverity.Critical,
                            new { cpgStatus = row.Status.ToString(), ordersStatus = external.ExecutionStatus }));
                    if (row.Status is GatewayPaymentStatus.Succeeded or GatewayPaymentStatus.PartiallyRefunded or GatewayPaymentStatus.Refunded && !captured)
                        obs.Add(Obs(ReconciliationFindingCodes.PaymentCaptureStateMismatch, ReconciliationSeverity.Critical,
                            new { cpgStatus = row.Status.ToString(), ordersStatus = external.ExecutionStatus }));
                    if (captured && row.WalletPaymentId is { } expected && external.PaymentId != expected)
                        obs.Add(Obs(ReconciliationFindingCodes.PaymentWalletReferenceMismatch, ReconciliationSeverity.Critical,
                            new { expectedWalletPaymentId = expected, observedWalletPaymentId = external.PaymentId }));
                    var stale = clock.GetUtcNow() - row.UpdatedAt >= TimeSpan.FromMinutes(configured.Value.ProcessingStaleAfterMinutes);
                    if (row.Status == GatewayPaymentStatus.Processing && row.ActiveAttemptId is not null && stale)
                        obs.Add(Obs(ReconciliationFindingCodes.PaymentProcessingStale, ReconciliationSeverity.Warning,
                            new { cpgStatus = row.Status.ToString(), ordersStatus = external.ExecutionStatus, attemptStatus = row.ActiveAttemptStatus?.ToString() }, "RecoverGatewayPaymentCommand"));
                }
            }
            var saved = await repository.ReconcileFindingsAsync(runId, ReconciliationKind.PaymentExecution,
                "GatewayPayment", row.PaymentId, row.Version, obs, ReconciliationResolutionCategory.ExternalConvergence,
                clock.GetUtcNow(), ct);
            if (!saved.SnapshotCurrent) continue; findings += obs.Count;
            if (!dryRun && row.ActiveAttemptId is { } attemptId)
                remediations += await Remediate(saved, obs, async () =>
                    await mediator.Send(new RecoverGatewayPaymentCommand(attemptId), ct), ct);
        }
        return (rows.Count, findings, remediations, errors, batch.Cursor);
    }

    private async Task<(int Scanned, int Findings, int Remediations, int Errors, string? Cursor)> Refunds(
        Guid runId, DateTimeOffset from, DateTimeOffset to, bool dryRun, CancellationToken ct)
    {
        var batch = await repository.GetRefundCandidatesAsync(from, to, configured.Value.BatchSize, ct); var rows = batch.Items;
        var findings = 0; var remediations = 0; var errors = 0;
        foreach (var row in rows)
        {
            var obs = new List<ReconciliationObservation>(); var now = clock.GetUtcNow();
            var stale = now - (row.LastCheckedAt ?? row.CreatedAt) >= TimeSpan.FromMinutes(configured.Value.RefundStaleAfterMinutes);
            if (row.Status is RefundStatus.Processing or RefundStatus.PendingRecovery && stale)
                obs.Add(Obs(ReconciliationFindingCodes.RefundRecoveryStale, ReconciliationSeverity.Warning,
                    new { cpgStatus = row.Status.ToString(), row.LastCheckedAt }, "ProcessRefundCommand"));
            if (row.Status == RefundStatus.Succeeded)
            {
                if (row.WalletPaymentId is null || row.WalletRefundId is null)
                    obs.Add(Obs(ReconciliationFindingCodes.RefundWalletReferenceMismatch, ReconciliationSeverity.Critical,
                        new { hasWalletPaymentId = row.WalletPaymentId is not null, hasWalletRefundId = row.WalletRefundId is not null }));
                else
                {
                    try
                    {
                        var response = await mediator.Send(new WalletRefundQuery(row.WalletPaymentId.Value, row.WalletRefundId.Value), ct);
                        var data = response.Data;
                        if (response.Status != CommandStatus.Completed || data is null || !string.Equals(data.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                            obs.Add(Obs(ReconciliationFindingCodes.RefundWalletStatusMismatch, ReconciliationSeverity.Critical,
                                new { cpgStatus = row.Status.ToString(), walletStatus = data?.Status ?? response.Status.ToString() }));
                        else
                        {
                            if (data.RefundId != row.WalletRefundId || data.PaymentId != row.WalletPaymentId)
                                obs.Add(Obs(ReconciliationFindingCodes.RefundWalletReferenceMismatch, ReconciliationSeverity.Critical,
                                    new { expectedRefundId = row.WalletRefundId, observedRefundId = data.RefundId, expectedPaymentId = row.WalletPaymentId, observedPaymentId = data.PaymentId }));
                            if (data.AmountMinor != row.AmountMinor || data.Currency != row.Currency)
                                obs.Add(Obs(ReconciliationFindingCodes.RefundAmountMismatch, ReconciliationSeverity.Critical,
                                    new { expectedAmountMinor = row.AmountMinor, observedAmountMinor = data.AmountMinor, expectedCurrency = row.Currency, observedCurrency = data.Currency }));
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch { errors++; continue; }
                }
                if (row.OrderProjectionStatus == OrderRefundProjectionStatus.Pending &&
                    now - (row.OrderProjectionNextRetryAt ?? row.CreatedAt) >= TimeSpan.FromMinutes(configured.Value.ProjectionStaleAfterMinutes))
                    obs.Add(Obs(ReconciliationFindingCodes.OrderFullRefundProjectionStale, ReconciliationSeverity.Warning,
                        new { projectionStatus = row.OrderProjectionStatus.ToString(), row.OrderProjectionNextRetryAt }, "ProcessRefundCommand"));
            }
            var saved = await repository.ReconcileFindingsAsync(runId, ReconciliationKind.Refund,
                "Refund", row.RefundId, row.Version, obs, ReconciliationResolutionCategory.ExternalConvergence,
                clock.GetUtcNow(), ct);
            if (!saved.SnapshotCurrent) continue; findings += obs.Count;
            if (!dryRun) remediations += await Remediate(saved, obs, async () =>
                await mediator.Send(new ProcessRefundCommand(row.RefundId), ct), ct);
        }
        return (rows.Count, findings, remediations, errors, batch.Cursor);
    }

    private async Task<int> Remediate(ReconciliationPersistResult saved,
        IReadOnlyCollection<ReconciliationObservation> observations, Func<Task> action, CancellationToken ct)
    {
        var options = configured.Value;
        if (!options.AutoRemediationEnabled || observations.Any(x => x.Severity == ReconciliationSeverity.Critical)) return 0;
        var candidate = observations.FirstOrDefault(x => x.AutoRemediationAction is not null);
        if (candidate is null || !saved.FindingIds.TryGetValue(candidate.Code, out var findingId)) return 0;
        if (!await repository.TryClaimAutoRemediationAsync(findingId, candidate.AutoRemediationAction!,
            options.MaxAutoRemediationAttempts, TimeSpan.FromSeconds(options.BaseRetrySeconds),
            TimeSpan.FromSeconds(options.MaximumRetrySeconds), clock.GetUtcNow(), ct)) return 0;
        await action(); return 1;
    }

    private void ValidateRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from is { } f && to is { } t && (t < f || t - f > TimeSpan.FromDays(configured.Value.MaximumHistoricalRangeDays)))
            throw new CreditPaymentsApplicationException("reconciliation_range_invalid", "بازه reconciliation نامعتبر است");
    }

    private static ReconciliationObservation Obs(string code, ReconciliationSeverity severity, object evidence, string? remediation = null)
    {
        var json = JsonSerializer.Serialize(evidence, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        return new(code, severity, json, hash, remediation);
    }

}

public sealed class WebhookReconciliationHandler(IWebhookIntegrityRemediator remediator) :
    IRequestHandler<ReconcileMissingWebhookEventsCommand, int>
{
    public Task<int> Handle(ReconcileMissingWebhookEventsCommand request, CancellationToken ct) =>
        remediator.ReconcileMissingEventsAsync(request.BatchSize, ct);
}

public sealed class CreditPaymentIntegrationEventReconciliationHandler(
    ICreditPaymentIntegrationEventReconciler reconciler) :
    IRequestHandler<ReconcileMissingCreditPaymentIntegrationEventsCommand, int>
{
    public Task<int> Handle(ReconcileMissingCreditPaymentIntegrationEventsCommand request, CancellationToken ct) =>
        reconciler.ReconcileAsync(request.BatchSize, ct);
}
