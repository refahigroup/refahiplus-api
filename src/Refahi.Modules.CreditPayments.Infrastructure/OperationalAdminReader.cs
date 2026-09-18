using Microsoft.EntityFrameworkCore;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts.IntegrationEvents;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public sealed class OperationalAdminReader(CreditPaymentsDbContext db,TimeProvider clock,
    IOptions<SettlementIntegrationOptions> settlement):ICreditPaymentsAdminReader
{
    public async Task<PagedAdminResult<MerchantDto>> SearchMerchantsAsync(SearchAdminMerchantsQuery r,CancellationToken ct)
    {
        var (page,size)=Page(r.Page,r.PageSize);var q=db.Merchants.AsNoTracking();
        if(r.MerchantId is not null)q=q.Where(x=>x.Id==r.MerchantId);if(r.SupplierId is not null)q=q.Where(x=>x.SupplierId==r.SupplierId);
        if(Enum.TryParse<MerchantStatus>(r.Status,true,out var status))q=q.Where(x=>x.Status==status);
        var total=await q.LongCountAsync(ct);var rows=await q.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Skip((page-1)*size).Take(size).ToListAsync(ct);
        return new(rows.Select(x=>new MerchantDto(x.Id,x.SupplierId,x.DisplayName,(short)x.Status,x.CreatedAt,x.UpdatedAt,x.Version)).ToArray(),page,size,total);
    }

    public async Task<PagedAdminResult<TerminalDto>> SearchTerminalsAsync(SearchAdminTerminalsQuery r,CancellationToken ct)
    {
        var (page,size)=Page(r.Page,r.PageSize);var q=db.Terminals.AsNoTracking().Include(x=>x.ReturnUrls).Include(x=>x.WebhookEndpoint).AsQueryable();
        if(r.TerminalId is not null)q=q.Where(x=>x.Id==r.TerminalId);if(r.MerchantId is not null)q=q.Where(x=>x.MerchantId==r.MerchantId);
        if(Enum.TryParse<TerminalStatus>(r.Status,true,out var status))q=q.Where(x=>x.Status==status);
        var total=await q.LongCountAsync(ct);var rows=await q.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Skip((page-1)*size).Take(size).ToListAsync(ct);
        return new(rows.Select(Terminal).ToArray(),page,size,total);
    }

    public async Task<PagedAdminResult<AdminPaymentSummaryDto>> SearchPaymentsAsync(SearchAdminPaymentsQuery r,CancellationToken ct)
    {
        var (page,size)=Page(r.Page,r.PageSize);var (from,to)=Range(r.From,r.To,r.PaymentId is not null);
        var q=db.GatewayPayments.AsNoTracking().Where(x=>x.CreatedAt>=from&&x.CreatedAt<=to);
        if(r.PaymentId is not null)q=q.Where(x=>x.Id==r.PaymentId);if(r.MerchantId is not null)q=q.Where(x=>x.MerchantId==r.MerchantId);
        if(r.TerminalId is not null)q=q.Where(x=>x.TerminalId==r.TerminalId);if(r.CustomerId is not null)q=q.Where(x=>x.CustomerId==r.CustomerId);
        if(!string.IsNullOrWhiteSpace(r.MerchantOrderId))q=q.Where(x=>x.MerchantOrderId==r.MerchantOrderId.Trim());
        if(Enum.TryParse<GatewayPaymentStatus>(r.Status,true,out var status))q=q.Where(x=>x.Status==status);
        var total=await q.LongCountAsync(ct);var rows=await q.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Skip((page-1)*size).Take(size).ToListAsync(ct);
        return new(rows.Select(Payment).ToArray(),page,size,total);
    }

    public async Task<AdminPaymentDetailsDto?> GetPaymentAsync(Guid id,CancellationToken ct)
    {
        var p=await db.GatewayPayments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);if(p is null)return null;
        var attempts=await db.PaymentAttempts.AsNoTracking().Where(x=>x.GatewayPaymentId==id).OrderBy(x=>x.AttemptSequence).ToListAsync(ct);
        var funding=await db.FundingContinuations.AsNoTracking().Where(x=>x.GatewayPaymentId==id).OrderBy(x=>x.CreatedAt).ToListAsync(ct);
        var termination=await db.PaymentTerminations.AsNoTracking().SingleOrDefaultAsync(x=>x.GatewayPaymentId==id,ct);
        var refunds=await db.Refunds.AsNoTracking().Where(x=>x.GatewayPaymentId==id).OrderByDescending(x=>x.CreatedAt).ToListAsync(ct);
        var events=await db.WebhookEvents.AsNoTracking().Where(x=>x.AggregateId==id||refunds.Select(y=>y.Id).Contains(x.AggregateId)).OrderByDescending(x=>x.OccurredAt).ToListAsync(ct);
        var eventIds=events.Select(x=>x.Id).ToArray();var deliveries=await db.WebhookDeliveries.AsNoTracking().Where(x=>eventIds.Contains(x.WebhookEventId)).OrderByDescending(x=>x.CreatedAt).ToListAsync(ct);
        return new(Payment(p),attempts.Select(Attempt).ToArray(),funding.Select(Funding).ToArray(),termination is null?null:Termination(termination),
            refunds.Select(Refund).ToArray(),events.Select(Event).ToArray(),deliveries.Select(Delivery).ToArray());
    }

    public async Task<PagedAdminResult<AdminRefundDto>> SearchRefundsAsync(SearchAdminRefundsQuery r,CancellationToken ct)
    {
        var (page,size)=Page(r.Page,r.PageSize);var (from,to)=Range(r.From,r.To,r.RefundId is not null);
        var q=db.Refunds.AsNoTracking().Where(x=>x.CreatedAt>=from&&x.CreatedAt<=to);
        if(r.RefundId is not null)q=q.Where(x=>x.Id==r.RefundId);if(r.PaymentId is not null)q=q.Where(x=>x.GatewayPaymentId==r.PaymentId);if(r.TerminalId is not null)q=q.Where(x=>x.TerminalId==r.TerminalId);
        if(r.MerchantId is not null)q=q.Where(x=>db.GatewayPayments.Any(p=>p.Id==x.GatewayPaymentId&&p.MerchantId==r.MerchantId));
        if(Enum.TryParse<RefundStatus>(r.Status,true,out var status))q=q.Where(x=>x.Status==status);
        var total=await q.LongCountAsync(ct);var rows=await q.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Skip((page-1)*size).Take(size).ToListAsync(ct);
        return new(rows.Select(Refund).ToArray(),page,size,total);
    }
    public async Task<AdminRefundDto?> GetRefundAsync(Guid id,CancellationToken ct){var x=await db.Refunds.AsNoTracking().SingleOrDefaultAsync(y=>y.Id==id,ct);return x is null?null:Refund(x);}

    public async Task<PagedAdminResult<AdminAuditDto>> SearchAuditAsync(SearchAdminAuditQuery r,CancellationToken ct)
    {
        var (page,size)=Page(r.Page,r.PageSize);var (from,to)=Range(r.From,r.To,r.EntityId is not null);var q=db.AuditRecords.AsNoTracking().Where(x=>x.OccurredAt>=from&&x.OccurredAt<=to);
        if(!string.IsNullOrWhiteSpace(r.EntityType))q=q.Where(x=>x.EntityType==r.EntityType);if(r.EntityId is not null)q=q.Where(x=>x.EntityId==r.EntityId);
        if(!string.IsNullOrWhiteSpace(r.Action))q=q.Where(x=>x.Action==r.Action);if(!string.IsNullOrWhiteSpace(r.Actor))q=q.Where(x=>x.ActorId==r.Actor);
        if(r.MerchantId is not null)q=q.Where(x=>x.MerchantId==r.MerchantId);if(r.TerminalId is not null)q=q.Where(x=>x.TerminalId==r.TerminalId);
        var total=await q.LongCountAsync(ct);var rows=await q.OrderByDescending(x=>x.OccurredAt).ThenByDescending(x=>x.Id).Skip((page-1)*size).Take(size).Select(x=>new AdminAuditDto(x.Id,x.Action,x.EntityType,x.EntityId,x.ActorId,x.MerchantId,x.TerminalId,x.MerchantOrderId,x.OccurredAt)).ToListAsync(ct);
        return new(rows,page,size,total);
    }

    public async Task<PagedAdminResult<RiskDecisionDto>> SearchRiskAsync(SearchRiskDecisionsQuery r,CancellationToken ct)
    {
        var (page,size)=Page(r.Page,r.PageSize);var (from,to)=Range(r.From,r.To,r.ResourceId is not null);var q=db.RiskDecisions.AsNoTracking().Where(x=>x.OccurredAt>=from&&x.OccurredAt<=to);
        if(r.ResourceId is not null)q=q.Where(x=>x.ResourceId==r.ResourceId);if(!string.IsNullOrWhiteSpace(r.RuleCode))q=q.Where(x=>x.RuleCode==r.RuleCode);
        if(!string.IsNullOrWhiteSpace(r.OperationType))q=q.Where(x=>x.OperationType==r.OperationType);if(Enum.TryParse<RiskDecision>(r.Decision,true,out var decision))q=q.Where(x=>x.Decision==decision);
        if(r.MerchantId is not null)q=q.Where(x=>x.MerchantId==r.MerchantId);if(r.TerminalId is not null)q=q.Where(x=>x.TerminalId==r.TerminalId);if(r.CustomerId is not null)q=q.Where(x=>x.CustomerId==r.CustomerId);
        var total=await q.LongCountAsync(ct);var rows=await q.OrderByDescending(x=>x.OccurredAt).ThenByDescending(x=>x.Id).Skip((page-1)*size).Take(size).ToListAsync(ct);
        return new(rows.Select(x=>new RiskDecisionDto(x.Id,x.OperationType,x.MerchantId,x.TerminalId,x.CustomerId,x.ResourceId,x.RuleCode,x.Decision.ToString().ToLowerInvariant(),x.CurrentValue,x.LimitValue,x.OccurredAt,x.CorrelationId)).ToArray(),page,size,total);
    }

    public async Task<AdminWebhookEventDto?> GetWebhookEventAsync(Guid id,CancellationToken ct){var x=await db.WebhookEvents.AsNoTracking().SingleOrDefaultAsync(y=>y.Id==id,ct);return x is null?null:Event(x);}
    public async Task<PagedAdminResult<AdminWebhookDeliveryDto>> SearchWebhookDeliveriesAsync(SearchAdminWebhookDeliveriesQuery r,CancellationToken ct)
    {
        var (page,size)=Page(r.Page,r.PageSize);var (from,to)=Range(r.From,r.To,r.EventId is not null);var q=db.WebhookDeliveries.AsNoTracking().Where(x=>x.CreatedAt>=from&&x.CreatedAt<=to);
        if(r.EventId is not null)q=q.Where(x=>x.WebhookEventId==r.EventId);if(Enum.TryParse<WebhookDeliveryStatus>(r.Status,true,out var status))q=q.Where(x=>x.Status==status);
        var total=await q.LongCountAsync(ct);var rows=await q.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Skip((page-1)*size).Take(size).ToListAsync(ct);
        var eventIds=rows.Select(x=>x.WebhookEventId).Distinct().ToArray();
        var events=await db.WebhookEvents.AsNoTracking().Where(x=>eventIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
        return new(rows.Select(x=>events.TryGetValue(x.WebhookEventId,out var e)?Delivery(x,e):Delivery(x)).ToArray(),page,size,total);
    }
    public async Task<PagedAdminResult<AdminIntegrationEventDto>> SearchIntegrationEventsAsync(SearchAdminIntegrationEventsQuery r,CancellationToken ct)
    {
        var (page,size)=Page(r.Page,r.PageSize);var (from,to)=Range(r.From,r.To,r.EventId is not null);
        var q=db.IntegrationEvents.AsNoTracking().Where(x=>x.OccurredAt>=from&&x.OccurredAt<=to);
        if(r.EventId is not null)q=q.Where(x=>x.Id==r.EventId);
        if(r.AggregateId is not null)q=q.Where(x=>x.AggregateId==r.AggregateId);
        if(!string.IsNullOrWhiteSpace(r.EventType))q=q.Where(x=>x.EventType==r.EventType.Trim());
        if(Enum.TryParse<CreditPaymentIntegrationEventStatus>(r.Status,true,out var status))q=q.Where(x=>x.Status==status);
        var total=await q.LongCountAsync(ct);
        var rows=await q.OrderByDescending(x=>x.OccurredAt).ThenByDescending(x=>x.Id).Skip((page-1)*size).Take(size)
            .Select(x=>new AdminIntegrationEventDto(x.Id,x.EventType,x.AggregateType,x.AggregateId,x.OccurredAt,
                x.Status.ToString().ToLowerInvariant(),x.RetryCount,x.NextRetryAt,x.PublishedAt,x.LastFailureCode)).ToListAsync(ct);
        return new(rows,page,size,total);
    }
    public async Task<PagedAdminResult<AdminCredentialMetadataDto>> ListCredentialsAsync(ListAdminCredentialMetadataQuery r,CancellationToken ct)
    {
        var (page,size)=Page(r.Page,r.PageSize);var q=db.TerminalCredentials.AsNoTracking().Where(x=>x.TerminalId==r.TerminalId);var total=await q.LongCountAsync(ct);var rows=await q.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Skip((page-1)*size).Take(size).ToListAsync(ct);
        return new(rows.Select(x=>new AdminCredentialMetadataDto(x.Id,x.TerminalId,x.ClientId,x.Label,x.Scopes.Split(' ',StringSplitOptions.RemoveEmptyEntries),x.Status.ToString().ToLowerInvariant(),x.CreatedAt,x.ExpiresAt,x.LastUsedAt,x.RevokedAt)).ToArray(),page,size,total);
    }
    public async Task<AdminOperationalSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var processing=await db.GatewayPayments.CountAsync(x=>x.Status==GatewayPaymentStatus.Processing,ct);var awaiting=await db.GatewayPayments.CountAsync(x=>x.Status==GatewayPaymentStatus.AwaitingFunding,ct);
        var pendingAttempts=await db.PaymentAttempts.CountAsync(x=>x.Status==PaymentAttemptStatus.PendingRecovery,ct);var pendingRefunds=await db.Refunds.CountAsync(x=>x.Status==RefundStatus.PendingRecovery,ct);
        var pendingFunding=await db.FundingContinuations.CountAsync(x=>x.Status==FundingContinuationStatus.PendingRecovery,ct);var pendingTerminations=await db.PaymentTerminations.CountAsync(x=>x.Status==PaymentTerminationStatus.PendingRecovery,ct);
        var dead=await db.WebhookDeliveries.CountAsync(x=>x.Status==WebhookDeliveryStatus.DeadLettered,ct);
        var oldest=await db.PaymentAttempts.Where(x=>x.Status==PaymentAttemptStatus.PendingRecovery).Select(x=>(DateTimeOffset?)x.StartedAt).MinAsync(ct);
        var openFindings=await db.ReconciliationFindings.CountAsync(x=>x.Status!=ReconciliationFindingStatus.Resolved,ct);
        var criticalFindings=await db.ReconciliationFindings.CountAsync(x=>x.Status!=ReconciliationFindingStatus.Resolved&&x.Severity==ReconciliationSeverity.Critical,ct);
        var oldestFinding=await db.ReconciliationFindings.Where(x=>x.Status!=ReconciliationFindingStatus.Resolved).Select(x=>(DateTimeOffset?)x.FirstObservedAt).MinAsync(ct);
        var lastRun=await db.ReconciliationRuns.AsNoTracking().OrderByDescending(x=>x.StartedAt).FirstOrDefaultAsync(ct);
        var unpublishedEvents=await db.IntegrationEvents.CountAsync(x=>x.Status!=CreditPaymentIntegrationEventStatus.Published,ct);
        var runDto=lastRun is null?null:new ReconciliationRunDto(lastRun.Id,lastRun.Kind.ToString(),lastRun.Status.ToString(),lastRun.StartedAt,lastRun.CompletedAt,lastRun.WorkerInstanceId,lastRun.DryRun,lastRun.ScannedCount,lastRun.FindingCount,lastRun.AutoRemediationCount,lastRun.ErrorCount,lastRun.FailureCode,lastRun.LeaseUntil);
        return new(processing,pendingAttempts,awaiting,pendingRefunds,pendingFunding,pendingTerminations,dead,oldest,openFindings,criticalFindings,oldestFinding,runDto,unpublishedEvents,settlement.Value.PublicationEnabled);
    }
    public async Task<AdminRecoveryTarget?> GetRecoveryTargetAsync(string type,Guid id,CancellationToken ct)
    {
        if(type is "payment" or "termination")
        {
            var paymentId=type=="payment"?id:(await db.PaymentTerminations.AsNoTracking().SingleOrDefaultAsync(x=>x.GatewayPaymentId==id||x.Id==id,ct))?.GatewayPaymentId??Guid.Empty;
            var attempt=await db.PaymentAttempts.AsNoTracking().Where(x=>x.GatewayPaymentId==paymentId&&x.Status==PaymentAttemptStatus.PendingRecovery).OrderByDescending(x=>x.StartedAt).FirstOrDefaultAsync(ct);
            return attempt is null?null:new(type,id,attempt.Id,attempt.Status.ToString());
        }
        if(type=="refund"){var x=await db.Refunds.AsNoTracking().SingleOrDefaultAsync(y=>y.Id==id&&y.Status==RefundStatus.PendingRecovery,ct);return x is null?null:new(type,id,null,x.Status.ToString());}
        if(type=="funding"){var x=await db.FundingContinuations.AsNoTracking().SingleOrDefaultAsync(y=>y.Id==id&&y.Status==FundingContinuationStatus.PendingRecovery,ct);return x is null?null:new(type,id,null,x.Status.ToString());}
        return null;
    }
    public async Task AuditRecoveryRequestAsync(string type,Guid id,string? actor,DateTimeOffset now,CancellationToken ct){db.AuditRecords.Add(AuditRecord.Create("admin_recovery_requested",type,id,actor,now));await db.SaveChangesAsync(ct);}

    private (DateTimeOffset From,DateTimeOffset To) Range(DateTimeOffset? from,DateTimeOffset? to,bool exact)
    {var end=to??clock.GetUtcNow();var start=from??(exact?DateTimeOffset.UnixEpoch:end.AddDays(-30));if(end<start||(!exact&&end-start>TimeSpan.FromDays(90)))throw new ArgumentException("بازه زمانی جست‌وجو نامعتبر است");return(start,end);}
    private static (int Page,int Size) Page(int page,int size)=>(Math.Max(1,page),Math.Clamp(size<=0?50:size,1,200));
    private static AdminPaymentSummaryDto Payment(GatewayPayment x)=>new(x.Id,x.MerchantId,x.TerminalId,x.MerchantOrderId,x.Status.ToString().ToLowerInvariant(),x.AmountMinor,x.Currency,x.CustomerId,x.OrderId,x.WalletPaymentId,x.CreatedAt,x.UpdatedAt);
    private static AdminAttemptDto Attempt(PaymentAttempt x)=>new(x.Id,x.AttemptSequence,x.Trigger.ToString(),x.Status.ToString(),x.NormalizedOutcomeCode,x.OrderId,x.WalletIntentId,x.WalletPaymentId,x.StartedAt,x.CompletedAt,x.RetryCount,x.NextRetryAt);
    private static AdminFundingDto Funding(FundingContinuation x)=>new(x.Id,x.Status.ToString(),x.CustomerId,x.PersonalWalletId,x.RequiredDeficitMinor,x.WalletFundingSessionId,x.RetryCount,x.CreatedAt,x.CompletedAt);
    private static AdminTerminationDto Termination(PaymentTermination x)=>new(x.Id,x.Kind.ToString(),x.Source.ToString(),x.Status.ToString(),x.Reason,x.RequestedAt,x.ResolvedAt,x.RetryCount);
    private static AdminRefundDto Refund(Refund x)=>new(x.Id,x.GatewayPaymentId,x.TerminalId,x.Status.ToString().ToLowerInvariant(),x.AmountMinor,x.Currency,x.MerchantRefundReference,x.NormalizedFailureCode,x.WalletRefundId,x.CreatedAt,x.CompletedAt,x.RetryCount);
    private static AdminWebhookEventDto Event(WebhookEvent x)=>new(x.Id,x.EventType,x.EventVersion,x.MerchantId,x.TerminalId,x.AggregateType,x.AggregateId,x.OccurredAt);
    private static AdminWebhookDeliveryDto Delivery(WebhookDelivery x)=>new(x.Id,x.WebhookEventId,x.Status.ToString().ToLowerInvariant(),x.AttemptCount,x.ManualResendCount,x.NextAttemptAt,x.LastAttemptAt,x.DeliveredAt,x.LastHttpStatusCode,x.LastFailureCode);
    private static AdminWebhookDeliveryDto Delivery(WebhookDelivery x,WebhookEvent e)=>new(x.Id,x.WebhookEventId,x.Status.ToString().ToLowerInvariant(),x.AttemptCount,x.ManualResendCount,x.NextAttemptAt,x.LastAttemptAt,x.DeliveredAt,x.LastHttpStatusCode,x.LastFailureCode,e.EventType,e.AggregateType,e.AggregateId);
    private static TerminalDto Terminal(Terminal x)=>new(x.Id,x.MerchantId,x.Name,(short)x.Status,x.ApprovedCategoryCode,x.MinPaymentAmountMinor,x.MaxPaymentAmountMinor,
        x.ReturnUrls.Select(y=>new ReturnUrlDto(y.Id,y.CanonicalUrl,y.CreatedAt)).ToArray(),x.WebhookEndpoint is null?null:new WebhookDto(x.WebhookEndpoint.Id,x.WebhookEndpoint.Url,(short)x.WebhookEndpoint.Status,x.WebhookEndpoint.SigningSecretVersion,x.WebhookEndpoint.CreatedAt,x.WebhookEndpoint.UpdatedAt),x.CreatedAt,x.UpdatedAt,x.Version);
}
