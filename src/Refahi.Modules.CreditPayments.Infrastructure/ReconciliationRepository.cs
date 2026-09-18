using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public sealed class ReconciliationRepository(CreditPaymentsDbContext db) : IReconciliationRepository
{
    public async Task<ReconciliationRun?> TryStartRunAsync(ReconciliationKind kind, string workerInstanceId,
        DateTimeOffset? from, DateTimeOffset? to, bool dryRun, DateTimeOffset leaseUntil,
        DateTimeOffset now, CancellationToken ct)
    {
        await db.ReconciliationRuns.Where(x => x.Kind == kind && x.Status == ReconciliationRunStatus.Running && x.LeaseUntil <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, ReconciliationRunStatus.Failed)
                .SetProperty(x => x.CompletedAt, now)
                .SetProperty(x => x.ErrorCount, 1)
                .SetProperty(x => x.FailureCode, "reconciliation_run_lease_expired")
                .SetProperty(x => x.Version, x => x.Version + 1), ct);
        var run = ReconciliationRun.Start(kind, workerInstanceId, from, to, dryRun, leaseUntil, now);
        db.ReconciliationRuns.Add(run);
        try { await db.SaveChangesAsync(ct); return run; }
        catch (DbUpdateException ex) when (Constraint(ex) == "ux_reconciliation_runs_active_kind")
        { db.ChangeTracker.Clear(); return null; }
    }

    public async Task CompleteRunAsync(Guid runId, int scanned, int findings, int remediations, int errors,
        string? cursor, DateTimeOffset now, CancellationToken ct)
    {
        var run = await db.ReconciliationRuns.SingleAsync(x => x.Id == runId, ct);
        run.Complete(scanned, findings, remediations, errors, cursor, now);
        await db.SaveChangesAsync(ct);
    }

    public async Task FailRunAsync(Guid runId, int scanned, int findings, int remediations, int errors,
        string failureCode, DateTimeOffset now, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var run = await db.ReconciliationRuns.SingleOrDefaultAsync(x => x.Id == runId, ct);
        if (run is null || run.Status != ReconciliationRunStatus.Running) return;
        run.Fail(scanned, findings, remediations, errors, failureCode, now);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ReconciliationBatch<FundingReconciliationSnapshot>> GetFundingCandidatesAsync(
        DateTimeOffset from, DateTimeOffset to, int batchSize, CancellationToken ct)
    {
        var fromDate = from;
        var cursor = await LastCursor(ReconciliationKind.Funding, ct); var parsed = ParseCursor(cursor);
        var q = from c in db.FundingContinuations.AsNoTracking()
                join p in db.GatewayPayments.AsNoTracking() on c.GatewayPaymentId equals p.Id
                where c.UpdatedAt >= fromDate && c.UpdatedAt <= to &&
                    (parsed == null || c.UpdatedAt > parsed.Value.At || (c.UpdatedAt == parsed.Value.At && c.Id.CompareTo(parsed.Value.Id) > 0))
                orderby c.UpdatedAt, c.Id
                select new FundingReconciliationSnapshot(c.Id, c.Version, c.GatewayPaymentId,
                    p.MerchantId, p.TerminalId, c.CustomerId, c.PersonalWalletId, c.RequiredDeficitMinor,
                    c.Currency, c.ClientReference, c.WalletFundingSessionId, c.Status, c.UpdatedAt);
        var rows = await q.Take(Math.Clamp(batchSize, 1, 500)).ToListAsync(ct);
        if (rows.Count == 0 && parsed is not null)
        {
            q = from c in db.FundingContinuations.AsNoTracking() join p in db.GatewayPayments.AsNoTracking()
                on c.GatewayPaymentId equals p.Id where c.UpdatedAt >= fromDate && c.UpdatedAt <= to
                orderby c.UpdatedAt, c.Id select new FundingReconciliationSnapshot(c.Id, c.Version, c.GatewayPaymentId,
                    p.MerchantId, p.TerminalId, c.CustomerId, c.PersonalWalletId, c.RequiredDeficitMinor,
                    c.Currency, c.ClientReference, c.WalletFundingSessionId, c.Status, c.UpdatedAt);
            rows = await q.Take(Math.Clamp(batchSize, 1, 500)).ToListAsync(ct);
        }
        return new(rows, rows.Count == 0 ? cursor : Cursor(rows[^1].UpdatedAt, rows[^1].ContinuationId));
    }

    public async Task<ReconciliationBatch<PaymentReconciliationSnapshot>> GetPaymentCandidatesAsync(
        DateTimeOffset from, DateTimeOffset to, int batchSize, CancellationToken ct)
    {
        var cursor = await LastCursor(ReconciliationKind.PaymentExecution, ct); var parsed = ParseCursor(cursor);
        var baseQuery = db.GatewayPayments.AsNoTracking()
            .Where(x => x.UpdatedAt >= from && x.UpdatedAt <= to &&
                (x.Status == GatewayPaymentStatus.Processing || x.Status == GatewayPaymentStatus.Succeeded ||
                 x.Status == GatewayPaymentStatus.PartiallyRefunded || x.Status == GatewayPaymentStatus.Refunded ||
                 x.Status == GatewayPaymentStatus.Canceled || x.Status == GatewayPaymentStatus.Expired));
        var payments = await baseQuery.Where(x => parsed == null || x.UpdatedAt > parsed.Value.At ||
                (x.UpdatedAt == parsed.Value.At && x.Id.CompareTo(parsed.Value.Id) > 0))
            .OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id).Take(Math.Clamp(batchSize, 1, 500)).ToListAsync(ct);
        if (payments.Count == 0 && parsed is not null)
            payments = await baseQuery.OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id)
                .Take(Math.Clamp(batchSize, 1, 500)).ToListAsync(ct);
        var ids = payments.Select(x => x.Id).ToArray();
        var attempts = await db.PaymentAttempts.AsNoTracking().Where(x => ids.Contains(x.GatewayPaymentId) &&
                (x.Status == PaymentAttemptStatus.Created || x.Status == PaymentAttemptStatus.Processing || x.Status == PaymentAttemptStatus.PendingRecovery))
            .OrderByDescending(x => x.StartedAt).ToListAsync(ct);
        var active = attempts.GroupBy(x => x.GatewayPaymentId).ToDictionary(x => x.Key, x => x.First());
        var rows = payments.Select(p =>
        {
            active.TryGetValue(p.Id, out var a);
            return new PaymentReconciliationSnapshot(p.Id, p.Version, p.MerchantId, p.TerminalId,
                p.CustomerId, p.OrderId, p.AmountMinor, p.Currency, p.Status, p.CapturedAmountMinor,
                p.RefundedAmountMinor, p.WalletPaymentId, p.PaidAt, p.UpdatedAt, a?.Id, a?.Status);
        }).ToArray();
        return new(rows, rows.Length == 0 ? cursor : Cursor(rows[^1].UpdatedAt, rows[^1].PaymentId));
    }

    public async Task<ReconciliationBatch<RefundReconciliationSnapshot>> GetRefundCandidatesAsync(
        DateTimeOffset from, DateTimeOffset to, int batchSize, CancellationToken ct)
    {
        var fromDate = from;
        var cursor = await LastCursor(ReconciliationKind.Refund, ct); var parsed = ParseCursor(cursor);
        var q = from r in db.Refunds.AsNoTracking()
                join p in db.GatewayPayments.AsNoTracking() on r.GatewayPaymentId equals p.Id
                where r.CreatedAt >= fromDate && r.CreatedAt <= to &&
                    (r.Status == RefundStatus.Processing || r.Status == RefundStatus.PendingRecovery ||
                     r.Status == RefundStatus.Succeeded) &&
                    (parsed == null || r.CreatedAt > parsed.Value.At || (r.CreatedAt == parsed.Value.At && r.Id.CompareTo(parsed.Value.Id) > 0))
                orderby r.CreatedAt, r.Id
                select new RefundReconciliationSnapshot(r.Id, r.Version, r.GatewayPaymentId,
                    p.MerchantId, p.TerminalId, p.CustomerId, p.OrderId, p.WalletPaymentId,
                    r.Status, r.AmountMinor, r.Currency, r.WalletRefundId, r.CreatedAt, r.LastCheckedAt,
                    r.OrderProjectionStatus, r.OrderProjectionNextRetryAt);
        var rows = await q.Take(Math.Clamp(batchSize, 1, 500)).ToListAsync(ct);
        if (rows.Count == 0 && parsed is not null)
        {
            q = from r in db.Refunds.AsNoTracking() join p in db.GatewayPayments.AsNoTracking()
                on r.GatewayPaymentId equals p.Id where r.CreatedAt >= fromDate && r.CreatedAt <= to &&
                (r.Status == RefundStatus.Processing || r.Status == RefundStatus.PendingRecovery || r.Status == RefundStatus.Succeeded)
                orderby r.CreatedAt, r.Id select new RefundReconciliationSnapshot(r.Id, r.Version, r.GatewayPaymentId,
                    p.MerchantId, p.TerminalId, p.CustomerId, p.OrderId, p.WalletPaymentId, r.Status, r.AmountMinor,
                    r.Currency, r.WalletRefundId, r.CreatedAt, r.LastCheckedAt, r.OrderProjectionStatus, r.OrderProjectionNextRetryAt);
            rows = await q.Take(Math.Clamp(batchSize, 1, 500)).ToListAsync(ct);
        }
        return new(rows, rows.Count == 0 ? cursor : Cursor(rows[^1].CreatedAt, rows[^1].RefundId));
    }

    public async Task<ReconciliationPersistResult> ReconcileFindingsAsync(Guid runId, ReconciliationKind kind,
        string resourceType, Guid resourceId, long expectedVersion,
        IReadOnlyCollection<ReconciliationObservation> observations,
        ReconciliationResolutionCategory resolutionCategory, DateTimeOffset now, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({resourceType + ':' + resourceId.ToString("N")}, 0))", ct);
        if (expectedVersion > 0 && await ReadVersion(resourceType, resourceId, ct) != expectedVersion)
        { await tx.RollbackAsync(ct); return new(0, 0, new Dictionary<string, Guid>(), false); }

        var active = await db.ReconciliationFindings.Where(x => x.Kind == kind &&
            x.ResourceType == resourceType && x.ResourceId == resourceId &&
            x.Status != ReconciliationFindingStatus.Resolved).ToListAsync(ct);
        var map = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var observedCodes = observations.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
        var opened = 0; var resolved = 0;
        foreach (var observation in observations)
        {
            var finding = active.SingleOrDefault(x => x.FindingCode == observation.Code);
            if (finding is null)
            {
                var ids = await Identity(resourceType, resourceId, ct);
                finding = ReconciliationFinding.Create(runId, kind, observation.Code, observation.Severity,
                    resourceType, resourceId, ids.MerchantId, ids.TerminalId, ids.CustomerId,
                    observation.EvidenceJson, observation.EvidenceHash, now);
                db.ReconciliationFindings.Add(finding); opened++;
                if (observation.Severity == ReconciliationSeverity.Critical)
                    db.AuditRecords.Add(AuditRecord.Create($"reconciliation.critical.{observation.Code}"[..Math.Min(100, $"reconciliation.critical.{observation.Code}".Length)],
                        "ReconciliationFinding", finding.Id, null, now));
            }
            else finding.Observe(runId, observation.Severity, observation.EvidenceJson, observation.EvidenceHash, now);
            map[observation.Code] = finding.Id;
        }
        foreach (var finding in active.Where(x => !observedCodes.Contains(x.FindingCode)))
        {
            finding.Resolve(resolutionCategory, runId, now); resolved++;
            db.AuditRecords.Add(AuditRecord.Create("reconciliation.finding-resolved",
                "ReconciliationFinding", finding.Id, null, now));
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(opened + observations.Count, resolved, map, true);
    }

    public async Task<bool> TryClaimAutoRemediationAsync(Guid findingId, string action, int maximumAttempts,
        TimeSpan baseDelay, TimeSpan maximumDelay, DateTimeOffset now, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var finding = await db.ReconciliationFindings.FromSqlInterpolated(
            $"SELECT * FROM credit_payments.reconciliation_findings WHERE reconciliation_finding_id={findingId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (finding is null || finding.Status == ReconciliationFindingStatus.Resolved ||
            finding.AutoRemediationAttemptCount >= maximumAttempts || finding.NextAutoRemediationAt > now)
        { await tx.CommitAsync(ct); return false; }
        var exponent = Math.Min(10, finding.AutoRemediationAttemptCount);
        var seconds = Math.Min(maximumDelay.TotalSeconds, baseDelay.TotalSeconds * Math.Pow(2, exponent));
        finding.RecordRemediation(action, now.AddSeconds(seconds), now);
        db.AuditRecords.Add(AuditRecord.Create("reconciliation.auto-remediation",
            "ReconciliationFinding", finding.Id, null, now));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }

    public async Task<(int Scanned, int Findings)> AuditInternalIntegrityAsync(Guid runId, DateTimeOffset from, DateTimeOffset to,
        int batchSize, DateTimeOffset now, CancellationToken ct)
    {
        var rows = await db.GatewayPayments.AsNoTracking().Where(x => x.UpdatedAt >= from && x.UpdatedAt <= to)
            .OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id).Take(Math.Clamp(batchSize, 1, 500)).ToListAsync(ct);
        var findings = 0;
        foreach (var p in rows)
        {
            var observations = new List<ReconciliationObservation>();
            void Add(string code, ReconciliationSeverity severity, object evidence) =>
                observations.Add(Observation(code, severity, evidence));
            if (p.Status is GatewayPaymentStatus.Succeeded or GatewayPaymentStatus.PartiallyRefunded or GatewayPaymentStatus.Refunded)
            {
                if (p.CapturedAmountMinor != p.AmountMinor || p.PaidAt is null || p.WalletPaymentId is null)
                    Add(ReconciliationFindingCodes.PaymentCaptureStateMismatch, ReconciliationSeverity.Critical,
                        new { cpgStatus = p.Status.ToString(), p.AmountMinor, p.CapturedAmountMinor, hasPaidAt = p.PaidAt is not null, hasWalletPaymentId = p.WalletPaymentId is not null });
            }
            var succeededRefundTotal = await db.Refunds.AsNoTracking().Where(x => x.GatewayPaymentId == p.Id && x.Status == RefundStatus.Succeeded)
                .SumAsync(x => (long?)x.AmountMinor, ct) ?? 0;
            var reservedRefundTotal = await db.Refunds.AsNoTracking().Where(x => x.GatewayPaymentId == p.Id &&
                (x.Status == RefundStatus.Succeeded || x.Status == RefundStatus.Processing || x.Status == RefundStatus.PendingRecovery))
                .SumAsync(x => (long?)x.AmountMinor, ct) ?? 0;
            if (succeededRefundTotal != p.RefundedAmountMinor)
                Add(ReconciliationFindingCodes.PaymentRefundedTotalMismatch, ReconciliationSeverity.Critical,
                    new { paymentRefundedAmountMinor = p.RefundedAmountMinor, succeededRefundTotal });
            if (reservedRefundTotal > p.CapturedAmountMinor)
                Add(ReconciliationFindingCodes.RefundAggregateMismatch, ReconciliationSeverity.Critical,
                    new { p.CapturedAmountMinor, reservedRefundTotal });

            var checkout = await db.CheckoutSessions.AsNoTracking().SingleOrDefaultAsync(x => x.GatewayPaymentId == p.Id, ct);
            if (checkout is not null && !CheckoutAligned(p.Status, checkout.Status))
                Add(ReconciliationFindingCodes.CheckoutStateMismatch, ReconciliationSeverity.Warning,
                    new { paymentStatus = p.Status.ToString(), checkoutStatus = checkout.Status.ToString() });
            var attemptCount = await db.PaymentAttempts.AsNoTracking().CountAsync(x => x.GatewayPaymentId == p.Id &&
                (x.Status == PaymentAttemptStatus.Created || x.Status == PaymentAttemptStatus.Processing || x.Status == PaymentAttemptStatus.PendingRecovery), ct);
            if (attemptCount > 1 || (p.Status == GatewayPaymentStatus.Succeeded && !await db.PaymentAttempts.AsNoTracking().AnyAsync(x => x.GatewayPaymentId == p.Id && x.Status == PaymentAttemptStatus.Succeeded, ct)))
                Add(ReconciliationFindingCodes.PaymentAttemptStateMismatch, ReconciliationSeverity.Critical,
                    new { paymentStatus = p.Status.ToString(), activeAttemptCount = attemptCount });
            var termination = await db.PaymentTerminations.AsNoTracking().SingleOrDefaultAsync(x => x.GatewayPaymentId == p.Id, ct);
            if (termination is not null && !TerminationAligned(p.Status, termination))
                Add(ReconciliationFindingCodes.TerminationStateMismatch, ReconciliationSeverity.Critical,
                    new { paymentStatus = p.Status.ToString(), terminationStatus = termination.Status.ToString(), terminationKind = termination.Kind.ToString() });
            var activeFunding = await db.FundingContinuations.AsNoTracking().CountAsync(x => x.GatewayPaymentId == p.Id &&
                (x.Status == FundingContinuationStatus.Created || x.Status == FundingContinuationStatus.RedirectReady || x.Status == FundingContinuationStatus.PendingRecovery), ct);
            if (activeFunding > 1 || await db.FundingContinuations.AsNoTracking().AnyAsync(x => x.GatewayPaymentId == p.Id && x.AutoResumeConsentAt == default, ct))
                Add(ReconciliationFindingCodes.FundingContinuationStateMismatch, ReconciliationSeverity.Critical,
                    new { activeFundingCount = activeFunding, consentInvariant = "required" });
            var saved = await ReconcileFindingsAsync(runId, ReconciliationKind.InternalIntegrity,
                "GatewayPayment", p.Id, p.Version, observations, ReconciliationResolutionCategory.ExternalConvergence, now, ct);
            if (saved.SnapshotCurrent) findings += observations.Count;
        }
        return (rows.Count, findings);
    }

    public async Task<(int Scanned, int Findings)> AuditWebhookIntegrityAsync(Guid runId, DateTimeOffset from, DateTimeOffset to,
        int batchSize, TimeSpan deadLetterStaleAfter, DateTimeOffset now, CancellationToken ct)
    {
        var findings = 0;
        var payments = await db.GatewayPayments.AsNoTracking().Where(x => x.UpdatedAt >= from && x.UpdatedAt <= to &&
            (x.Status == GatewayPaymentStatus.Succeeded || x.Status == GatewayPaymentStatus.PartiallyRefunded ||
             x.Status == GatewayPaymentStatus.Refunded || x.Status == GatewayPaymentStatus.Failed ||
             x.Status == GatewayPaymentStatus.Canceled || x.Status == GatewayPaymentStatus.Expired))
            .OrderBy(x => x.UpdatedAt).Take(Math.Clamp(batchSize, 1, 500)).ToListAsync(ct);
        foreach (var p in payments)
        {
            var type = p.Status switch
            {
                GatewayPaymentStatus.Succeeded or GatewayPaymentStatus.PartiallyRefunded or GatewayPaymentStatus.Refunded => WebhookEventTypes.PaymentSucceeded,
                GatewayPaymentStatus.Failed => WebhookEventTypes.PaymentFailed,
                GatewayPaymentStatus.Canceled => WebhookEventTypes.PaymentCanceled,
                _ => WebhookEventTypes.PaymentExpired
            };
            var source = $"{type}:{p.Id:N}";
            var ev = await db.WebhookEvents.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKey == source, ct);
            var obs = new List<ReconciliationObservation>();
            if (ev is null) obs.Add(Observation(ReconciliationFindingCodes.MissingWebhookEvent,
                ReconciliationSeverity.Warning, new { eventType = type, aggregateId = p.Id }));
            else if (!await db.OutboxMessages.AsNoTracking().AnyAsync(x => x.WebhookEventId == ev.Id, ct))
                obs.Add(Observation(ReconciliationFindingCodes.MissingOutbox, ReconciliationSeverity.Critical,
                    new { eventId = ev.Id, eventType = ev.EventType }));
            var financialEventSource = $"credit-payment-captured:{p.Id:N}";
            if (!await db.IntegrationEvents.AsNoTracking().AnyAsync(x => x.SourceKey == financialEventSource, ct))
            {
                obs.Add(Observation(ReconciliationFindingCodes.MissingFinancialCaptureEvent,
                    ReconciliationSeverity.Warning, new { gatewayPaymentId = p.Id,
                        occurredAt = p.PaidAt, eventType = "credit-payment-captured.v1" },
                    "CreditPaymentIntegrationEventReconciliationWorker"));
                if (p.PaidAt is null || p.OrderId is null || p.WalletPaymentId is null ||
                    p.PolicyId is null || p.PolicyVersion is null || p.PolicyHash is null)
                    obs.Add(Observation(ReconciliationFindingCodes.FinancialEventContractDataInvalid,
                        ReconciliationSeverity.Critical, new { gatewayPaymentId = p.Id,
                            hasPaidAt = p.PaidAt is not null, hasOrderId = p.OrderId is not null,
                            hasWalletPaymentId = p.WalletPaymentId is not null,
                            hasPolicy = p.PolicyId is not null && p.PolicyVersion is not null && p.PolicyHash is not null }));
            }
            var saved = await ReconcileFindingsAsync(runId, ReconciliationKind.WebhookIntegrity,
                "GatewayPayment", p.Id, p.Version, obs, ReconciliationResolutionCategory.WebhookReconciliation, now, ct);
            if (saved.SnapshotCurrent) findings += obs.Count;
        }
        var refunds = await db.Refunds.AsNoTracking().Where(x => x.CompletedAt >= from && x.CompletedAt <= to &&
                (x.Status == RefundStatus.Succeeded || x.Status == RefundStatus.Failed))
            .OrderBy(x => x.CompletedAt).Take(Math.Clamp(batchSize, 1, 500)).ToListAsync(ct);
        foreach (var r in refunds)
        {
            var type = r.Status == RefundStatus.Succeeded ? WebhookEventTypes.RefundSucceeded : WebhookEventTypes.RefundFailed;
            var source = $"{type}:{r.Id:N}";
            var ev = await db.WebhookEvents.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKey == source, ct);
            var obs = new List<ReconciliationObservation>();
            if (ev is null) obs.Add(Observation(ReconciliationFindingCodes.MissingWebhookEvent,
                ReconciliationSeverity.Warning, new { eventType = type, aggregateId = r.Id }));
            else if (!await db.OutboxMessages.AsNoTracking().AnyAsync(x => x.WebhookEventId == ev.Id, ct))
                obs.Add(Observation(ReconciliationFindingCodes.MissingOutbox, ReconciliationSeverity.Critical,
                    new { eventId = ev.Id, eventType = ev.EventType }));
            var financialEventSource = $"credit-payment-refunded:{r.Id:N}";
            if (!await db.IntegrationEvents.AsNoTracking().AnyAsync(x => x.SourceKey == financialEventSource, ct))
            {
                obs.Add(Observation(ReconciliationFindingCodes.MissingFinancialRefundEvent,
                    ReconciliationSeverity.Warning, new { refundId = r.Id,
                        occurredAt = r.CompletedAt, eventType = "credit-payment-refunded.v1" },
                    "CreditPaymentIntegrationEventReconciliationWorker"));
                var payment = await db.GatewayPayments.AsNoTracking().SingleAsync(x => x.Id == r.GatewayPaymentId, ct);
                if (r.CompletedAt is null || r.WalletRefundId is null || payment.OrderId is null || payment.WalletPaymentId is null)
                    obs.Add(Observation(ReconciliationFindingCodes.FinancialEventContractDataInvalid,
                        ReconciliationSeverity.Critical, new { refundId = r.Id,
                            hasCompletedAt = r.CompletedAt is not null, hasWalletRefundId = r.WalletRefundId is not null,
                            hasOrderId = payment.OrderId is not null, hasWalletPaymentId = payment.WalletPaymentId is not null }));
            }
            var saved = await ReconcileFindingsAsync(runId, ReconciliationKind.WebhookIntegrity,
                "Refund", r.Id, r.Version, obs, ReconciliationResolutionCategory.WebhookReconciliation, now, ct);
            if (saved.SnapshotCurrent) findings += obs.Count;
        }
        var deadLetters = await db.WebhookDeliveries.AsNoTracking().Where(x => x.Status == WebhookDeliveryStatus.DeadLettered &&
                x.UpdatedAt <= now.Subtract(deadLetterStaleAfter))
            .OrderBy(x => x.UpdatedAt).Take(Math.Clamp(batchSize, 1, 500)).ToListAsync(ct);
        foreach (var d in deadLetters)
        {
            var obs = new[] { Observation(ReconciliationFindingCodes.WebhookDeliveryDeadLettered,
                ReconciliationSeverity.Warning, new { deliveryId = d.Id, eventId = d.WebhookEventId, d.LastFailureCode, d.AttemptCount }) };
            await ReconcileFindingsAsync(runId, ReconciliationKind.WebhookIntegrity,
                "WebhookDelivery", d.Id, d.Version, obs, ReconciliationResolutionCategory.OperationalAction, now, ct);
            findings++;
        }
        return (payments.Count + refunds.Count + deadLetters.Count, findings);
    }

    public async Task<(int Scanned, int Findings, int Repairs)> AuditRiskIntegrityAsync(Guid runId, int batchSize,
        bool allowRepair, DateTimeOffset now, CancellationToken ct)
    {
        var counters = await db.RiskVelocityCounters.AsNoTracking().OrderBy(x => x.UpdatedAt)
            .Take(Math.Clamp(batchSize, 1, 500)).ToListAsync(ct);
        var findings = 0; var repairs = 0;
        foreach (var snapshot in counters)
        {
            db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(ct);
            var counter = await db.RiskVelocityCounters.FromSqlInterpolated(
                $"SELECT * FROM credit_payments.risk_velocity_counters WHERE risk_velocity_counter_id={snapshot.Id} FOR UPDATE").SingleAsync(ct);
            var reservations = await db.RiskReservations.Where(x => x.CounterId == counter.Id && x.ReleasedAt == null).ToListAsync(ct);
            var ambiguous = false;
            foreach (var r in reservations)
            {
                ambiguous |= r.OperationType switch
                {
                    RiskOperationTypes.PaymentCreate or RiskOperationTypes.PaymentExecution => !await db.GatewayPayments.AnyAsync(x => x.Id == r.ResourceId, ct),
                    RiskOperationTypes.RefundCreate => !await db.Refunds.AnyAsync(x => x.Id == r.ResourceId, ct),
                    RiskOperationTypes.FundingStart => !await db.FundingContinuations.AnyAsync(x => x.Id == r.ResourceId, ct),
                    _ => true
                };
            }
            var expectedCount = reservations.LongCount(); var expectedAmount = reservations.Sum(x => x.AmountMinor);
            var observations = new List<ReconciliationObservation>();
            if (ambiguous) observations.Add(Observation(ReconciliationFindingCodes.RiskReservationMismatch,
                ReconciliationSeverity.Critical, new { counterId = counter.Id, reason = "missing_or_unknown_resource" }));
            if (counter.AcceptedCount != expectedCount || counter.AcceptedAmountMinor != expectedAmount)
                observations.Add(Observation(ReconciliationFindingCodes.RiskCounterDrift,
                    ambiguous ? ReconciliationSeverity.Critical : ReconciliationSeverity.Warning,
                    new { counterId = counter.Id, actualCount = counter.AcceptedCount, expectedCount,
                        actualAmountMinor = counter.AcceptedAmountMinor, expectedAmount }));
            var shouldRepair = allowRepair && !ambiguous && observations.Any(x => x.Code == ReconciliationFindingCodes.RiskCounterDrift);
            if (shouldRepair)
            {
                counter.Rebuild(expectedCount, expectedAmount, now); repairs++;
                db.AuditRecords.Add(AuditRecord.Create("reconciliation.risk-counter-rebuilt", "RiskVelocityCounter", counter.Id, null, now));
            }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            var saved = await ReconcileFindingsAsync(runId, ReconciliationKind.RiskIntegrity,
                "RiskVelocityCounter", counter.Id, counter.Version, observations,
                ReconciliationResolutionCategory.ExternalConvergence,
                now, ct);
            if (shouldRepair && saved.SnapshotCurrent)
                await ReconcileFindingsAsync(runId, ReconciliationKind.RiskIntegrity,
                    "RiskVelocityCounter", counter.Id, counter.Version, [],
                    ReconciliationResolutionCategory.RiskCounterRebuild, now, ct);
            findings += observations.Count;
        }
        return (counters.Count, findings, repairs);
    }

    public async Task<bool> AcknowledgeFindingAsync(Guid findingId, string actor, string? note,
        DateTimeOffset now, CancellationToken ct)
    {
        var finding = await db.ReconciliationFindings.SingleOrDefaultAsync(x => x.Id == findingId, ct);
        if (finding is null || finding.Status == ReconciliationFindingStatus.Resolved) return false;
        finding.Acknowledge(actor, note, now);
        db.AuditRecords.Add(AuditRecord.Create("reconciliation.finding-acknowledged", "ReconciliationFinding", finding.Id, actor, now));
        await db.SaveChangesAsync(ct); return true;
    }

    private async Task<long?> ReadVersion(string type, Guid id, CancellationToken ct) => type switch
    {
        "GatewayPayment" => await db.GatewayPayments.AsNoTracking().Where(x => x.Id == id).Select(x => (long?)x.Version).SingleOrDefaultAsync(ct),
        "FundingContinuation" => await db.FundingContinuations.AsNoTracking().Where(x => x.Id == id).Select(x => (long?)x.Version).SingleOrDefaultAsync(ct),
        "Refund" => await db.Refunds.AsNoTracking().Where(x => x.Id == id).Select(x => (long?)x.Version).SingleOrDefaultAsync(ct),
        "WebhookDelivery" => await db.WebhookDeliveries.AsNoTracking().Where(x => x.Id == id).Select(x => (long?)x.Version).SingleOrDefaultAsync(ct),
        "RiskVelocityCounter" => await db.RiskVelocityCounters.AsNoTracking().Where(x => x.Id == id).Select(x => (long?)x.Version).SingleOrDefaultAsync(ct),
        _ => null
    };

    private async Task<(Guid? MerchantId, Guid? TerminalId, Guid? CustomerId)> Identity(string type, Guid id, CancellationToken ct)
    {
        if (type == "GatewayPayment")
            return await db.GatewayPayments.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new ValueTuple<Guid?, Guid?, Guid?>(x.MerchantId, x.TerminalId, x.CustomerId)).SingleAsync(ct);
        if (type == "FundingContinuation")
            return await (from c in db.FundingContinuations.AsNoTracking() join p in db.GatewayPayments.AsNoTracking()
                          on c.GatewayPaymentId equals p.Id where c.Id == id
                          select new ValueTuple<Guid?, Guid?, Guid?>(p.MerchantId, p.TerminalId, c.CustomerId)).SingleAsync(ct);
        if (type == "Refund")
            return await (from r in db.Refunds.AsNoTracking() join p in db.GatewayPayments.AsNoTracking()
                          on r.GatewayPaymentId equals p.Id where r.Id == id
                          select new ValueTuple<Guid?, Guid?, Guid?>(p.MerchantId, p.TerminalId, p.CustomerId)).SingleAsync(ct);
        return (null, null, null);
    }

    internal static ReconciliationObservation Observation(string code, ReconciliationSeverity severity, object evidence,
        string? remediation = null)
    {
        var json = JsonSerializer.Serialize(evidence, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        if (Encoding.UTF8.GetByteCount(json) > 8192) json = "{\"evidence\":\"bounded\"}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        return new(code, severity, json, hash, remediation);
    }

    private static bool CheckoutAligned(GatewayPaymentStatus payment, CheckoutSessionStatus checkout) => payment switch
    {
        GatewayPaymentStatus.Succeeded or GatewayPaymentStatus.PartiallyRefunded or GatewayPaymentStatus.Refunded => checkout == CheckoutSessionStatus.Completed,
        GatewayPaymentStatus.Canceled => checkout == CheckoutSessionStatus.Canceled,
        GatewayPaymentStatus.Expired => checkout == CheckoutSessionStatus.Expired,
        GatewayPaymentStatus.Processing => checkout == CheckoutSessionStatus.Processing,
        _ => true
    };

    private static bool TerminationAligned(GatewayPaymentStatus payment, PaymentTermination termination) => termination.Status switch
    {
        PaymentTerminationStatus.CaptureWon => payment is GatewayPaymentStatus.Succeeded or GatewayPaymentStatus.PartiallyRefunded or GatewayPaymentStatus.Refunded,
        PaymentTerminationStatus.Completed when termination.Kind == PaymentTerminationKind.Cancel => payment == GatewayPaymentStatus.Canceled,
        PaymentTerminationStatus.Completed when termination.Kind == PaymentTerminationKind.Expire => payment == GatewayPaymentStatus.Expired,
        _ => true
    };

    private static string? Constraint(DbUpdateException ex) => (ex.InnerException as PostgresException)?.ConstraintName;
    private async Task<string?> LastCursor(ReconciliationKind kind, CancellationToken ct) => await db.ReconciliationRuns.AsNoTracking()
        .Where(x => x.Kind == kind && x.Status != ReconciliationRunStatus.Running && x.Cursor != null)
        .OrderByDescending(x => x.CompletedAt).Select(x => x.Cursor).FirstOrDefaultAsync(ct);
    private static string Cursor(DateTimeOffset at, Guid id) => $"{at.UtcTicks}:{id:N}";
    private static (DateTimeOffset At, Guid Id)? ParseCursor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split(':', 2);
        return parts.Length == 2 && long.TryParse(parts[0], out var ticks) && Guid.TryParseExact(parts[1], "N", out var id)
            ? (new DateTimeOffset(ticks, TimeSpan.Zero), id) : null;
    }
}

public sealed class ExistingWebhookIntegrityRemediator(WebhookEventReconciler reconciler) : IWebhookIntegrityRemediator
{
    public Task<int> ReconcileMissingEventsAsync(int batchSize, CancellationToken ct) => reconciler.ExecuteOnceAsync(batchSize, ct);
}
