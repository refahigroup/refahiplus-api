using Microsoft.EntityFrameworkCore;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public sealed class ReconciliationAdminReader(CreditPaymentsDbContext db, TimeProvider clock) : IReconciliationAdminReader
{
    public async Task<PagedAdminResult<ReconciliationRunDto>> SearchRunsAsync(SearchReconciliationRunsQuery r, CancellationToken ct)
    {
        var (page, size) = Page(r.Page, r.PageSize); var (from, to) = Range(r.From, r.To);
        var q = db.ReconciliationRuns.AsNoTracking().Where(x => x.StartedAt >= from && x.StartedAt <= to);
        if (Enum.TryParse<ReconciliationKind>(r.Kind, true, out var kind)) q = q.Where(x => x.Kind == kind);
        if (Enum.TryParse<ReconciliationRunStatus>(r.Status, true, out var status)) q = q.Where(x => x.Status == status);
        var total = await q.LongCountAsync(ct);
        var rows = await q.OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(Map).ToArray(), page, size, total);
    }

    public async Task<ReconciliationRunDto?> GetRunAsync(Guid id, CancellationToken ct)
    {
        var row = await db.ReconciliationRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : Map(row);
    }

    public async Task<PagedAdminResult<ReconciliationFindingDto>> SearchFindingsAsync(SearchReconciliationFindingsQuery r, CancellationToken ct)
    {
        var (page, size) = Page(r.Page, r.PageSize); var (from, to) = Range(r.From, r.To);
        var q = db.ReconciliationFindings.AsNoTracking().Where(x => x.LastObservedAt >= from && x.LastObservedAt <= to);
        if (!string.IsNullOrWhiteSpace(r.Code)) q = q.Where(x => x.FindingCode == r.Code.Trim());
        if (!string.IsNullOrWhiteSpace(r.ResourceType)) q = q.Where(x => x.ResourceType == r.ResourceType.Trim());
        if (r.ResourceId is not null) q = q.Where(x => x.ResourceId == r.ResourceId);
        if (r.MerchantId is not null) q = q.Where(x => x.MerchantId == r.MerchantId);
        if (r.TerminalId is not null) q = q.Where(x => x.TerminalId == r.TerminalId);
        if (Enum.TryParse<ReconciliationSeverity>(r.Severity, true, out var severity)) q = q.Where(x => x.Severity == severity);
        if (Enum.TryParse<ReconciliationFindingStatus>(r.Status, true, out var status)) q = q.Where(x => x.Status == status);
        var total = await q.LongCountAsync(ct);
        var rows = await q.OrderByDescending(x => x.LastObservedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(Map).ToArray(), page, size, total);
    }

    public async Task<ReconciliationFindingDto?> GetFindingAsync(Guid id, CancellationToken ct)
    {
        var row = await db.ReconciliationFindings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : Map(row);
    }

    private (DateTimeOffset From, DateTimeOffset To) Range(DateTimeOffset? from, DateTimeOffset? to)
    {
        var end = to ?? clock.GetUtcNow(); var start = from ?? end.AddDays(-30);
        if (end < start || end - start > TimeSpan.FromDays(90)) throw new ArgumentException("بازه زمانی جست‌وجو نامعتبر است");
        return (start, end);
    }
    private static (int Page, int Size) Page(int page, int size) => (Math.Max(1, page), Math.Clamp(size <= 0 ? 50 : size, 1, 200));
    private static ReconciliationRunDto Map(ReconciliationRun x) => new(x.Id, x.Kind.ToString(), x.Status.ToString(),
        x.StartedAt, x.CompletedAt, x.WorkerInstanceId, x.DryRun, x.ScannedCount, x.FindingCount,
        x.AutoRemediationCount, x.ErrorCount, x.FailureCode, x.LeaseUntil);
    private static ReconciliationFindingDto Map(ReconciliationFinding x) => new(x.Id, x.ReconciliationRunId,
        x.Kind.ToString(), x.FindingCode, x.Severity.ToString(), x.ResourceType, x.ResourceId, x.MerchantId,
        x.TerminalId, x.CustomerId, x.Status.ToString(), x.FirstObservedAt, x.LastObservedAt, x.ObservationCount,
        x.ResolvedAt, x.ResolutionCategory == ReconciliationResolutionCategory.None ? null : x.ResolutionCategory.ToString(),
        x.AcknowledgedAt, x.AcknowledgedBy, x.AcknowledgementNote, x.AutoRemediationAction,
        x.AutoRemediationAttemptCount, x.NextAutoRemediationAt, x.EvidenceJson);
}
