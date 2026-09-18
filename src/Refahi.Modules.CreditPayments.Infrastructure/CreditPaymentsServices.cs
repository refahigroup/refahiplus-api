using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public sealed class CreditPaymentsRepository(CreditPaymentsDbContext db):ICreditPaymentsRepository,IGatewayPaymentRepository,ICheckoutRepository,IFundingContinuationRepository,IPaymentExecutionRepository,IPaymentTerminationRepository,IRefundRepository,IWebhookOperationsRepository
{
    public Task<Merchant?> GetMerchantAsync(Guid id,bool tracking,CancellationToken ct)=>Query(db.Merchants,tracking).Include(x=>x.Terminals).SingleOrDefaultAsync(x=>x.Id==id,ct);
    public Task<Merchant?> GetMerchantBySupplierAsync(Guid supplierId,CancellationToken ct)=>db.Merchants.AsNoTracking().SingleOrDefaultAsync(x=>x.SupplierId==supplierId,ct);
    public Task<Terminal?> GetTerminalAsync(Guid id,bool tracking,CancellationToken ct)=>Query(db.Terminals,tracking).Include(x=>x.ReturnUrls).Include(x=>x.Credentials).Include(x=>x.WebhookEndpoint).SingleOrDefaultAsync(x=>x.Id==id,ct);
    public Task<TerminalCredential?> GetCredentialAsync(Guid id,bool tracking,CancellationToken ct)=>Query(db.TerminalCredentials,tracking).SingleOrDefaultAsync(x=>x.Id==id,ct);
    public async Task<(TerminalCredential Credential,Terminal Terminal,Merchant Merchant)?> GetAuthenticationGraphAsync(string clientId,CancellationToken ct){var credential=await db.TerminalCredentials.SingleOrDefaultAsync(x=>x.ClientId==clientId,ct);if(credential is null)return null;var terminal=await db.Terminals.SingleOrDefaultAsync(x=>x.Id==credential.TerminalId,ct);if(terminal is null)return null;var merchant=await db.Merchants.SingleOrDefaultAsync(x=>x.Id==terminal.MerchantId,ct);return merchant is null?null:(credential,terminal,merchant);}
    public Task<GatewayPayment?> GetGatewayPaymentAsync(Guid paymentId,Guid merchantId,Guid terminalId,CancellationToken ct)=>db.GatewayPayments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==paymentId&&x.MerchantId==merchantId&&x.TerminalId==terminalId,ct);
    public Task<IdempotencyRecord?> GetIdempotencyRecordAsync(Guid terminalId,string operationType,string idempotencyKey,CancellationToken ct)=>db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x=>x.TerminalId==terminalId&&x.OperationType==operationType&&x.IdempotencyKey==idempotencyKey,ct);
    public async Task<PaymentCreationPersistenceResult> CreateGatewayPaymentAsync(
        GatewayPayment payment,CheckoutSession checkoutSession,IdempotencyRecord idempotency,
        IReadOnlyCollection<AuditRecord> audits,RiskReservationRequest? risk,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.IdempotencyRecords.Add(idempotency);
            await db.SaveChangesAsync(ct);
            if(risk is not null)
            {
                var riskResult=await ReserveRiskAsync(risk,payment.CreatedAt,ct);
                if(!riskResult.Allowed)
                {
                    db.IdempotencyRecords.Remove(idempotency);
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return new(PaymentCreationPersistenceOutcome.RiskDenied,null,null,riskResult.RuleCode);
                }
            }
            db.GatewayPayments.Add(payment);
            db.CheckoutSessions.Add(checkoutSession);
            db.AuditRecords.AddRange(audits);
            idempotency.Complete(payment.Id,payment.CreatedAt);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(PaymentCreationPersistenceOutcome.Created,payment,checkoutSession);
        }
        catch(DbUpdateException ex) when(IsUnique(ex,out var constraint))
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            if(constraint=="ux_gateway_payments_terminal_order")
                return new(PaymentCreationPersistenceOutcome.MerchantOrderConflict,null,null);
            if(constraint!="ux_idempotency_terminal_operation_key") throw;
            var existing=await db.IdempotencyRecords.AsNoTracking().SingleAsync(x=>x.TerminalId==idempotency.TerminalId&&x.OperationType==idempotency.OperationType&&x.IdempotencyKey==idempotency.IdempotencyKey,ct);
            if(existing.RequestHashVersion!=idempotency.RequestHashVersion||!CryptographicOperations.FixedTimeEquals(existing.RequestHash,idempotency.RequestHash))
                return new(PaymentCreationPersistenceOutcome.IdempotencyConflict,null,null);
            if(existing.State!=IdempotencyState.Completed||existing.ResourceId is null)
                throw new InvalidOperationException("An idempotency owner committed without a completed resource binding.");
            var existingPayment=await db.GatewayPayments.AsNoTracking().SingleAsync(x=>x.Id==existing.ResourceId.Value,ct);
            var existingCheckout=await db.CheckoutSessions.AsNoTracking().SingleOrDefaultAsync(x=>x.GatewayPaymentId==existingPayment.Id&&
                (x.Status==CheckoutSessionStatus.Created||x.Status==CheckoutSessionStatus.Authenticated||
                 x.Status==CheckoutSessionStatus.AwaitingFunding||x.Status==CheckoutSessionStatus.Processing),ct)
                ?? throw new InvalidOperationException("A completed idempotency resource has no active checkout session.");
            return new(PaymentCreationPersistenceOutcome.Existing,existingPayment,existingCheckout);
        }
    }
    public Task<PaymentCreationPersistenceResult> CreateGatewayPaymentAsync(GatewayPayment payment,
        CheckoutSession checkoutSession,IdempotencyRecord idempotency,IReadOnlyCollection<AuditRecord> audits,
        CancellationToken ct)=>CreateGatewayPaymentAsync(payment,checkoutSession,idempotency,audits,null,ct);
    public async Task<CheckoutLookup?> FindCheckoutAsync(byte[] tokenHash,bool tracking,CancellationToken ct)
    {
        var session=await Query(db.CheckoutSessions,tracking).SingleOrDefaultAsync(x=>x.TokenHash==tokenHash,ct);
        if(session is null)return null;
        var payment=await Query(db.GatewayPayments,tracking).SingleOrDefaultAsync(x=>x.Id==session.GatewayPaymentId,ct);
        return payment is null?null:new(session,payment);
    }
    public async Task<FundingEvaluationContext?> GetFundingEvaluationContextAsync(
        byte[] tokenHash,
        CancellationToken ct)
    {
        var session=await db.CheckoutSessions.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.TokenHash==tokenHash,ct);
        if(session is null)return null;
        var payment=await db.GatewayPayments.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.Id==session.GatewayPaymentId,ct);
        if(payment is null)return null;
        var merchant=await db.Merchants.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.Id==payment.MerchantId,ct);
        return merchant is null?null:new FundingEvaluationContext(session,payment,merchant);
    }
    public async Task<PaymentExecutionContext?> GetPaymentExecutionContextAsync(byte[] tokenHash,CancellationToken ct)
    {
        var session=await db.CheckoutSessions.AsNoTracking().SingleOrDefaultAsync(x=>x.TokenHash==tokenHash,ct);
        return session is null?null:await GetPaymentExecutionContextAsync(session.GatewayPaymentId,ct);
    }
    public async Task<PaymentExecutionContext?> GetPaymentExecutionContextAsync(Guid gatewayPaymentId,CancellationToken ct)
    {
        var payment=await db.GatewayPayments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==gatewayPaymentId,ct);
        if(payment is null)return null;
        var session=await db.CheckoutSessions.AsNoTracking()
            .OrderByDescending(x=>x.CreatedAt).FirstOrDefaultAsync(x=>x.GatewayPaymentId==gatewayPaymentId,ct);
        if(session is null)return null;
        var attempt=await db.PaymentAttempts.AsNoTracking().SingleOrDefaultAsync(x=>x.GatewayPaymentId==gatewayPaymentId&&
            (x.Status==PaymentAttemptStatus.Created||x.Status==PaymentAttemptStatus.Processing||x.Status==PaymentAttemptStatus.PendingRecovery),ct);
        var continuation=await db.FundingContinuations.AsNoTracking().Where(x=>x.GatewayPaymentId==gatewayPaymentId)
            .OrderByDescending(x=>x.CreatedAt).FirstOrDefaultAsync(ct);
        var termination=await db.PaymentTerminations.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.GatewayPaymentId==gatewayPaymentId,ct);
        return new(session,payment,attempt,continuation,termination);
    }
    public async Task<PaymentExecutionContext?> GetPaymentAttemptContextAsync(Guid paymentAttemptId,CancellationToken ct)
    {
        var attempt=await db.PaymentAttempts.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==paymentAttemptId,ct);
        return attempt is null?null:await GetPaymentExecutionContextAsync(attempt.GatewayPaymentId,ct);
    }
    public async Task<PaymentExecutionClaimResult> ClaimPaymentExecutionAsync(
        Guid gatewayPaymentId,PaymentAttemptTrigger trigger,string policyId,string policyVersion,
        string policyHash,Guid destinationWalletId,RiskReservationRequest? risk,DateTimeOffset now,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var payment=await db.GatewayPayments.FromSqlInterpolated(
            $"SELECT * FROM credit_payments.gateway_payments WHERE gateway_payment_id = {gatewayPaymentId} FOR UPDATE")
            .SingleAsync(ct);
        var session=await db.CheckoutSessions.FromSqlInterpolated(
            $"SELECT * FROM credit_payments.checkout_sessions WHERE gateway_payment_id = {gatewayPaymentId} ORDER BY created_at DESC LIMIT 1 FOR UPDATE")
            .SingleAsync(ct);
        var active=await db.PaymentAttempts.SingleOrDefaultAsync(x=>x.GatewayPaymentId==gatewayPaymentId&&
            (x.Status==PaymentAttemptStatus.Created||x.Status==PaymentAttemptStatus.Processing||x.Status==PaymentAttemptStatus.PendingRecovery),ct);
        var latestFunding=await db.FundingContinuations.Where(x=>x.GatewayPaymentId==gatewayPaymentId)
            .OrderByDescending(x=>x.CreatedAt).FirstOrDefaultAsync(ct);
        if(payment.Status==GatewayPaymentStatus.Succeeded)
        {
            await transaction.CommitAsync(ct);
            return new(PaymentExecutionClaimOutcome.Succeeded,new(session,payment,active,latestFunding));
        }
        if(payment.Status==GatewayPaymentStatus.Processing||active is not null)
        {
            await transaction.CommitAsync(ct);
            return new(PaymentExecutionClaimOutcome.Processing,new(session,payment,active,latestFunding));
        }
        if(payment.Status!=GatewayPaymentStatus.AwaitingCustomer||session.Status!=CheckoutSessionStatus.Authenticated
            ||payment.CustomerId is null||payment.OrderId is null||session.CustomerId!=payment.CustomerId
            ||now>=payment.ExpiresAt||now>=session.ExpiresAt)
        {
            await transaction.CommitAsync(ct);
            return new(PaymentExecutionClaimOutcome.NotReady,new(session,payment,active,latestFunding));
        }
        if(risk is not null)
        {
            var riskResult=await ReserveRiskAsync(risk,now,ct);
            if(!riskResult.Allowed)
            {
                await transaction.CommitAsync(ct);
                return new(PaymentExecutionClaimOutcome.RiskDenied,new(session,payment,active,latestFunding),riskResult.RuleCode);
            }
        }
        var sequence=(await db.PaymentAttempts.Where(x=>x.GatewayPaymentId==gatewayPaymentId)
            .Select(x=>(int?)x.AttemptSequence).MaxAsync(ct)??0)+1;
        var attempt=PaymentAttempt.Create(payment.Id,sequence,trigger,payment.OrderId.Value,
            policyId,policyVersion,policyHash,destinationWalletId,now);
        payment.BeginProcessing(policyId,policyVersion,policyHash,now);
        session.BeginProcessing(now);
        db.PaymentAttempts.Add(attempt);
        db.AuditRecords.Add(AuditRecord.Create(
            trigger switch
            {
                PaymentAttemptTrigger.CustomerConfirmation=>"gateway-payment.customer-confirmed",
                PaymentAttemptTrigger.FundingAutoResume=>"gateway-payment.auto-resume-executed",
                _=>"gateway-payment.recovery-executed"
            },
            "GatewayPayment",payment.Id,payment.CustomerId.Value.ToString("D"),now));
        db.AuditRecords.Add(AuditRecord.Create("gateway-payment.policy-resolved",
            "PaymentAttempt",attempt.Id,payment.CustomerId.Value.ToString("D"),now));
        db.AuditRecords.Add(AuditRecord.Create("gateway-payment.execution-claimed","PaymentAttempt",attempt.Id,
            payment.CustomerId.Value.ToString("D"),now));
        db.AuditRecords.Add(AuditRecord.Create("gateway-payment.orders-execution-requested","PaymentAttempt",attempt.Id,
            payment.CustomerId.Value.ToString("D"),now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(PaymentExecutionClaimOutcome.Claimed,new(session,payment,attempt,latestFunding));
    }
    public Task<PaymentExecutionClaimResult> ClaimPaymentExecutionAsync(Guid gatewayPaymentId,
        PaymentAttemptTrigger trigger,string policyId,string policyVersion,string policyHash,
        Guid destinationWalletId,DateTimeOffset now,CancellationToken ct)=>ClaimPaymentExecutionAsync(
            gatewayPaymentId,trigger,policyId,policyVersion,policyHash,destinationWalletId,null,now,ct);
    public async Task<PaymentExecutionContext> FinalizePaymentSuccessAsync(Guid paymentAttemptId,Guid? walletIntentId,
        Guid walletPaymentId,DateTimeOffset paidAt,DateTimeOffset now,CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var attempt=await LockPaymentAttempt(paymentAttemptId,ct);
        var payment=await LockPayment(attempt.GatewayPaymentId,ct);
        var session=await LockCheckout(payment.Id,ct);
        if(payment.Status!=GatewayPaymentStatus.Succeeded)
        {
            payment.MarkSucceeded(walletPaymentId,paidAt,now);
            session.Complete(now);
            attempt.MarkSucceeded(walletIntentId,walletPaymentId,now);
            WebhookEventFactory.AddPayment(db,payment,WebhookEventTypes.PaymentSucceeded,paidAt,now);
            var merchant=await db.Merchants.SingleAsync(x=>x.Id==payment.MerchantId,ct);
            var integrationEvent=SettlementIntegrationEventFactory.Captured(payment,merchant.SupplierId,now);
            db.IntegrationEvents.Add(integrationEvent);
            db.AuditRecords.Add(AuditRecord.Create("settlement-integration.fact-created",
                "IntegrationEvent",integrationEvent.Id,null,now));
            var termination=await db.PaymentTerminations.SingleOrDefaultAsync(
                x=>x.GatewayPaymentId==payment.Id,ct);
            termination?.MarkCaptureWon(now);
            db.AuditRecords.Add(AuditRecord.Create("gateway-payment.capture-succeeded","GatewayPayment",payment.Id,
                payment.CustomerId?.ToString("D"),now));
            if(termination is not null)
                db.AuditRecords.Add(AuditRecord.Create("gateway-payment.termination-capture-won",
                    "PaymentTermination",termination.Id,payment.CustomerId?.ToString("D"),now));
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new(session,payment,attempt,null);
    }
    public async Task<PaymentExecutionContext> FinalizePaymentInsufficientAsync(Guid paymentAttemptId,DateTimeOffset now,CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var attempt=await LockPaymentAttempt(paymentAttemptId,ct);
        var payment=await LockPayment(attempt.GatewayPaymentId,ct);
        var session=await LockCheckout(payment.Id,ct);
        var termination=await db.PaymentTerminations.SingleOrDefaultAsync(
            x=>x.GatewayPaymentId==payment.Id,ct);
        if(payment.Status==GatewayPaymentStatus.Processing)
        {
            attempt.MarkFailed("insufficient_funds",now);
            if(payment.CustomerId is { } customerId)
                await ReleaseRiskAsync(RiskScopeType.Customer,customerId,
                    RiskOperationTypes.PaymentExecution,payment.Id,now,ct);
            if(termination is { IsUnresolved:true })
            {
                FinalizeLocalTermination(payment,session,termination,now);
                AddTerminationWebhook(payment,termination,now);
                db.AuditRecords.Add(AuditRecord.Create(
                    termination.Kind==PaymentTerminationKind.Cancel
                        ?"gateway-payment.canceled-finalized":"gateway-payment.expired-finalized",
                    "PaymentTermination",termination.Id,payment.CustomerId?.ToString("D"),now));
            }
            else
            {
                payment.ReturnToAwaitingFunding(now);
                session.ReturnToAwaitingFunding(now);
                db.AuditRecords.Add(AuditRecord.Create("gateway-payment.execution-insufficient","PaymentAttempt",attempt.Id,
                    payment.CustomerId?.ToString("D"),now));
            }
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new(session,payment,attempt,null,termination);
    }
    public async Task<PaymentExecutionContext> FinalizePaymentNoEffectAsync(Guid paymentAttemptId,string outcomeCode,DateTimeOffset now,CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var attempt=await LockPaymentAttempt(paymentAttemptId,ct);
        var payment=await LockPayment(attempt.GatewayPaymentId,ct);
        var session=await LockCheckout(payment.Id,ct);
        var termination=await db.PaymentTerminations.SingleOrDefaultAsync(
            x=>x.GatewayPaymentId==payment.Id,ct);
        if(payment.Status==GatewayPaymentStatus.Processing)
        {
            attempt.MarkFailed(outcomeCode,now);
            if(payment.CustomerId is { } customerId)
                await ReleaseRiskAsync(RiskScopeType.Customer,customerId,
                    RiskOperationTypes.PaymentExecution,payment.Id,now,ct);
            if(termination is { IsUnresolved:true })
            {
                FinalizeLocalTermination(payment,session,termination,now);
                AddTerminationWebhook(payment,termination,now);
                db.AuditRecords.Add(AuditRecord.Create(
                    termination.Kind==PaymentTerminationKind.Cancel
                        ?"gateway-payment.canceled-finalized":"gateway-payment.expired-finalized",
                    "PaymentTermination",termination.Id,payment.CustomerId?.ToString("D"),now));
            }
            else
            {
                payment.ReturnToAwaitingCustomer(now);
                session.ReturnToAuthenticated(now);
            }
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new(session,payment,attempt,null,termination);
    }
    public async Task MarkPaymentPendingRecoveryAsync(Guid paymentAttemptId,DateTimeOffset now,DateTimeOffset nextRetryAt,CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var attempt=await LockPaymentAttempt(paymentAttemptId,ct);
        attempt.MarkPendingRecovery(now,nextRetryAt);
        db.AuditRecords.Add(AuditRecord.Create("gateway-payment.outcome-unknown","PaymentAttempt",attempt.Id,null,now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    public async Task RecordPaymentExecutionAuditAsync(Guid gatewayPaymentId,Guid? actorId,string action,
        DateTimeOffset now,CancellationToken ct)
    {
        db.AuditRecords.Add(AuditRecord.Create(action,"GatewayPayment",gatewayPaymentId,
            actorId?.ToString("D"),now));
        await db.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<Guid>> LeasePaymentRecoveryBatchAsync(string leaseOwner,DateTimeOffset now,
        DateTimeOffset leaseUntil,int batchSize,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var attempts=await db.PaymentAttempts.FromSqlInterpolated($@"SELECT * FROM credit_payments.payment_attempts
            WHERE status IN (2,3) AND (next_retry_at IS NULL OR next_retry_at <= {now})
              AND (lease_until IS NULL OR lease_until <= {now})
            ORDER BY COALESCE(next_retry_at, started_at), started_at
            FOR UPDATE SKIP LOCKED LIMIT {batchSize}").ToListAsync(ct);
        foreach(var attempt in attempts)attempt.AcquireLease(leaseOwner,leaseUntil,now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return attempts.Select(x=>x.Id).ToArray();
    }
    public async Task<IReadOnlyList<Guid>> LeaseAutomaticResumeBatchAsync(string leaseOwner,DateTimeOffset now,
        DateTimeOffset leaseUntil,int batchSize,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var continuations=await db.FundingContinuations.FromSqlInterpolated($@"SELECT fc.*
            FROM credit_payments.funding_continuations fc
            JOIN credit_payments.gateway_payments gp ON gp.gateway_payment_id=fc.gateway_payment_id
            JOIN credit_payments.checkout_sessions cs ON cs.checkout_session_id=fc.checkout_session_id
            WHERE fc.status=4 AND fc.auto_resume_consent_at IS NOT NULL
              AND gp.status=2 AND cs.status=2
              AND gp.expires_at>{now} AND cs.expires_at>{now}
              AND (fc.lease_until IS NULL OR fc.lease_until<={now})
            ORDER BY fc.completed_at,fc.created_at
            FOR UPDATE OF fc SKIP LOCKED LIMIT {batchSize}").ToListAsync(ct);
        foreach(var continuation in continuations)continuation.AcquireAutomaticResumeLease(leaseOwner,leaseUntil,now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return continuations.Select(x=>x.GatewayPaymentId).ToArray();
    }
    private Task<PaymentAttempt> LockPaymentAttempt(Guid id,CancellationToken ct)=>db.PaymentAttempts.FromSqlInterpolated(
        $"SELECT * FROM credit_payments.payment_attempts WHERE payment_attempt_id={id} FOR UPDATE").SingleAsync(ct);
    private Task<GatewayPayment> LockPayment(Guid id,CancellationToken ct)=>db.GatewayPayments.FromSqlInterpolated(
        $"SELECT * FROM credit_payments.gateway_payments WHERE gateway_payment_id={id} FOR UPDATE").SingleAsync(ct);
    private Task<CheckoutSession> LockCheckout(Guid paymentId,CancellationToken ct)=>db.CheckoutSessions.FromSqlInterpolated(
        $"SELECT * FROM credit_payments.checkout_sessions WHERE gateway_payment_id={paymentId} ORDER BY created_at DESC LIMIT 1 FOR UPDATE").SingleAsync(ct);
    public async Task BindOrderAsync(
        Guid checkoutSessionId,
        Guid gatewayPaymentId,
        Guid customerId,
        Guid orderId,
        DateTimeOffset now,
        bool recovered,
        CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var session=await db.CheckoutSessions
            .FromSqlInterpolated($"SELECT * FROM credit_payments.checkout_sessions WHERE checkout_session_id = {checkoutSessionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw new CheckoutStateException("checkout_not_found","نشست پرداخت یافت نشد");
        var payment=await db.GatewayPayments
            .FromSqlInterpolated($"SELECT * FROM credit_payments.gateway_payments WHERE gateway_payment_id = {gatewayPaymentId} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw new CheckoutStateException("checkout_not_found","نشست پرداخت یافت نشد");
        EnsureFundingContext(session,payment,customerId,orderId:null,now);
        var wasBound=payment.OrderId.HasValue;
        payment.BindOrder(orderId,now);
        if(!wasBound)
        {
            db.AuditRecords.Add(AuditRecord.Create(
                recovered?"gateway-payment.order-binding-recovered":"gateway-payment.order-bound",
                "GatewayPayment",payment.Id,customerId.ToString("D"),now));
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    public async Task<FundingStatePersistenceResult> ApplyFundingEvaluationAsync(
        Guid checkoutSessionId,
        Guid gatewayPaymentId,
        Guid customerId,
        Guid orderId,
        bool isSufficient,
        long eligibleAmountMinor,
        long deficitAmountMinor,
        DateTimeOffset evaluatedAt,
        CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var session=await db.CheckoutSessions
            .FromSqlInterpolated($"SELECT * FROM credit_payments.checkout_sessions WHERE checkout_session_id = {checkoutSessionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw new CheckoutStateException("checkout_not_found","نشست پرداخت یافت نشد");
        var payment=await db.GatewayPayments
            .FromSqlInterpolated($"SELECT * FROM credit_payments.gateway_payments WHERE gateway_payment_id = {gatewayPaymentId} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw new CheckoutStateException("checkout_not_found","نشست پرداخت یافت نشد");
        EnsureFundingContext(session,payment,customerId,orderId,evaluatedAt);
        var wasAwaitingFunding=payment.Status==GatewayPaymentStatus.AwaitingFunding;
        if(isSufficient)
        {
            payment.MarkFundingSufficient(evaluatedAt);
            session.MarkFundingSufficient(evaluatedAt);
        }
        else
        {
            payment.MarkAwaitingFunding(evaluatedAt);
            session.MarkAwaitingFunding(evaluatedAt);
        }
        db.AuditRecords.Add(AuditRecord.Create(
            "gateway-payment.funding-eligibility-evaluated",
            "GatewayPayment",payment.Id,customerId.ToString("D"),evaluatedAt));
        if(!isSufficient)
            db.AuditRecords.Add(AuditRecord.Create(
                "gateway-payment.insufficient-funding",
                "GatewayPayment",payment.Id,customerId.ToString("D"),evaluatedAt));
        else if(wasAwaitingFunding)
            db.AuditRecords.Add(AuditRecord.Create(
                "gateway-payment.funding-became-sufficient",
                "GatewayPayment",payment.Id,customerId.ToString("D"),evaluatedAt));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new FundingStatePersistenceResult(session,payment,wasAwaitingFunding&&isSufficient);
    }
    public async Task RecordFundingIntegrityFailureAsync(
        Guid gatewayPaymentId,
        Guid customerId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        db.AuditRecords.Add(AuditRecord.Create(
            "gateway-payment.funding-integrity-mismatch",
            "GatewayPayment",gatewayPaymentId,customerId.ToString("D"),now));
        await db.SaveChangesAsync(ct);
    }
    public async Task<FundingContinuationClaimResult> CreateOrGetFundingContinuationAsync(
        Guid checkoutSessionId,Guid gatewayPaymentId,Guid customerId,Guid orderId,
        Guid personalWalletId,long requiredDeficitMinor,int maximumAttempts,
        RiskReservationRequest? risk,DateTimeOffset now,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var session=await db.CheckoutSessions
            .FromSqlInterpolated($"SELECT * FROM credit_payments.checkout_sessions WHERE checkout_session_id = {checkoutSessionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw new CheckoutStateException("checkout_not_found","نشست پرداخت یافت نشد");
        var payment=await db.GatewayPayments
            .FromSqlInterpolated($"SELECT * FROM credit_payments.gateway_payments WHERE gateway_payment_id = {gatewayPaymentId} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw new CheckoutStateException("checkout_not_found","نشست پرداخت یافت نشد");
        EnsureFundingContext(session,payment,customerId,orderId,now);
        var active=await db.FundingContinuations.SingleOrDefaultAsync(x=>
            x.GatewayPaymentId==gatewayPaymentId &&
            (x.Status==FundingContinuationStatus.Created
             ||x.Status==FundingContinuationStatus.RedirectReady
             ||x.Status==FundingContinuationStatus.PendingRecovery),ct);
        if(active is not null)
        {
            await transaction.CommitAsync(ct);
            return new(FundingContinuationClaimOutcome.Existing,active);
        }
        var attemptCount=await db.FundingContinuations.CountAsync(x=>
            x.GatewayPaymentId==gatewayPaymentId&&x.FundingStartedAt!=null,ct);
        if(attemptCount>=maximumAttempts)
        {
            await transaction.CommitAsync(ct);
            return new(FundingContinuationClaimOutcome.AttemptLimitReached,null);
        }
        var continuation=FundingContinuation.Create(gatewayPaymentId,checkoutSessionId,
            customerId,personalWalletId,requiredDeficitMinor,now);
        if(risk is not null)
        {
            risk=risk with { ResourceId=continuation.Id };
            var riskResult=await ReserveRiskAsync(risk,now,ct);
            if(!riskResult.Allowed)
            {
                await transaction.CommitAsync(ct);
                return new(FundingContinuationClaimOutcome.RiskDenied,null,riskResult.RuleCode);
            }
        }
        db.FundingContinuations.Add(continuation);
        db.AuditRecords.Add(AuditRecord.Create("funding-continuation.created",
            "FundingContinuation",continuation.Id,customerId.ToString("D"),now));
        db.AuditRecords.Add(AuditRecord.Create("funding-continuation.auto-resume-consented",
            "FundingContinuation",continuation.Id,customerId.ToString("D"),now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(FundingContinuationClaimOutcome.Created,continuation);
    }
    public Task<FundingContinuationClaimResult> CreateOrGetFundingContinuationAsync(Guid checkoutSessionId,
        Guid gatewayPaymentId,Guid customerId,Guid orderId,Guid personalWalletId,long requiredDeficitMinor,
        int maximumAttempts,DateTimeOffset now,CancellationToken ct)=>CreateOrGetFundingContinuationAsync(
            checkoutSessionId,gatewayPaymentId,customerId,orderId,personalWalletId,requiredDeficitMinor,
            maximumAttempts,null,now,ct);
    public async Task<FundingContinuationContext?> GetFundingContinuationContextAsync(byte[] tokenHash,CancellationToken ct)
    {
        var session=await db.CheckoutSessions.AsNoTracking().SingleOrDefaultAsync(x=>x.TokenHash==tokenHash,ct);
        if(session is null)return null;
        var payment=await db.GatewayPayments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==session.GatewayPaymentId,ct);
        if(payment is null)return null;
        var continuation=await db.FundingContinuations.AsNoTracking()
            .Where(x=>x.GatewayPaymentId==payment.Id)
            .OrderByDescending(x=>x.CreatedAt).FirstOrDefaultAsync(ct);
        return continuation is null?null:new(session,payment,continuation);
    }
    public async Task<FundingContinuationContext?> GetFundingContinuationContextAsync(Guid fundingContinuationId,CancellationToken ct)
    {
        var continuation=await db.FundingContinuations.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==fundingContinuationId,ct);
        if(continuation is null)return null;
        var session=await db.CheckoutSessions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==continuation.CheckoutSessionId,ct);
        var payment=await db.GatewayPayments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==continuation.GatewayPaymentId,ct);
        return session is null||payment is null?null:new(session,payment,continuation);
    }
    public async Task BindWalletFundingSessionAsync(Guid fundingContinuationId,Guid walletFundingSessionId,DateTimeOffset now,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var continuation=await LockContinuation(fundingContinuationId,ct);
        continuation.BindSession(walletFundingSessionId,now);
        db.AuditRecords.Add(AuditRecord.Create("funding-continuation.session-bound",
            "FundingContinuation",continuation.Id,continuation.CustomerId.ToString("D"),now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    public async Task MarkFundingContinuationPendingAsync(Guid fundingContinuationId,DateTimeOffset now,DateTimeOffset nextRetryAt,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var continuation=await LockContinuation(fundingContinuationId,ct);
        continuation.MarkPendingRecovery(now,nextRetryAt);
        db.AuditRecords.Add(AuditRecord.Create("funding-continuation.pending-recovery",
            "FundingContinuation",continuation.Id,continuation.CustomerId.ToString("D"),now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    public async Task CompleteFundingContinuationAsync(Guid fundingContinuationId,FundingContinuationStatus status,DateTimeOffset now,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var continuation=await LockContinuation(fundingContinuationId,ct);
        switch(status)
        {
            case FundingContinuationStatus.Succeeded: continuation.MarkSucceeded(now); break;
            case FundingContinuationStatus.Failed: continuation.MarkFailed(now); break;
            case FundingContinuationStatus.Canceled: continuation.MarkCanceled(now); break;
            default: throw new ArgumentOutOfRangeException(nameof(status));
        }
        db.AuditRecords.Add(AuditRecord.Create($"funding-continuation.{status.ToString().ToLowerInvariant()}",
            "FundingContinuation",continuation.Id,continuation.CustomerId.ToString("D"),now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    public async Task SchedulePostFundingEvaluationAsync(Guid fundingContinuationId,DateTimeOffset now,DateTimeOffset nextRetryAt,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var continuation=await LockContinuation(fundingContinuationId,ct);
        continuation.SchedulePostFundingEvaluation(now,nextRetryAt);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    public async Task CompletePostFundingEvaluationAsync(Guid fundingContinuationId,DateTimeOffset now,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var continuation=await LockContinuation(fundingContinuationId,ct);
        continuation.CompletePostFundingEvaluation(now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<Guid>> LeaseFundingContinuationRecoveryBatchAsync(
        string leaseOwner,DateTimeOffset now,DateTimeOffset leaseUntil,int batchSize,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var candidates=await db.FundingContinuations
            .FromSqlInterpolated($@"SELECT * FROM credit_payments.funding_continuations
                WHERE (status IN (1,2,3) OR (status = 4 AND next_retry_at IS NOT NULL))
                  AND (next_retry_at IS NULL OR next_retry_at <= {now})
                  AND (lease_until IS NULL OR lease_until <= {now})
                ORDER BY COALESCE(next_retry_at, created_at), created_at
                FOR UPDATE SKIP LOCKED LIMIT {batchSize}")
            .ToListAsync(ct);
        foreach(var item in candidates)item.AcquireLease(leaseOwner,leaseUntil,now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return candidates.Select(x=>x.Id).ToArray();
    }
    private Task<FundingContinuation> LockContinuation(Guid id,CancellationToken ct)=>
        db.FundingContinuations.FromSqlInterpolated(
            $"SELECT * FROM credit_payments.funding_continuations WHERE funding_continuation_id = {id} FOR UPDATE")
            .SingleAsync(ct);
    public Task<CheckoutSession?> GetCheckoutForPaymentAsync(Guid paymentId,CancellationToken ct)=>
        db.CheckoutSessions.AsNoTracking().SingleOrDefaultAsync(x=>x.GatewayPaymentId==paymentId&&
            (x.Status==CheckoutSessionStatus.Created||x.Status==CheckoutSessionStatus.Authenticated||
             x.Status==CheckoutSessionStatus.AwaitingFunding||x.Status==CheckoutSessionStatus.Processing||
             x.Status==CheckoutSessionStatus.Completed),ct);
    public async Task<OtpChallengeDispatch> ReserveOtpChallengeAsync(byte[] tokenHash,Guid? resolvedCustomerId,short otpType,
        DateTimeOffset now,DateTimeOffset provisionalExpiresAt,TimeSpan resendCooldown,int maximumSends,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var session=await db.CheckoutSessions.FromSqlInterpolated($"SELECT * FROM credit_payments.checkout_sessions WHERE token_hash = {tokenHash} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new CheckoutStateException("checkout_not_found","نشست پرداخت یافت نشد");
        var payment=await db.GatewayPayments.SingleAsync(x=>x.Id==session.GatewayPaymentId,ct);
        EnsureCheckoutUsable(session,payment,now);
        if(session.Status==CheckoutSessionStatus.Authenticated)
            throw new CheckoutStateException("checkout_already_authenticated","نشست پرداخت قبلاً احراز هویت شده است");
        var challenges=await db.CheckoutOtpChallenges.Where(x=>x.CheckoutSessionId==session.Id)
            .OrderByDescending(x=>x.SendSequence).ToListAsync(ct);
        if(challenges.Count>=maximumSends||challenges.FirstOrDefault() is { } latest&&now<latest.SentAt.Add(resendCooldown))
            throw new CheckoutStateException("otp_rate_limited","درخواست کد تأیید بیش از حد مجاز است");
        var expiry=provisionalExpiresAt<=session.ExpiresAt?provisionalExpiresAt:session.ExpiresAt;
        var challenge=CheckoutOtpChallenge.Reserve(session.Id,resolvedCustomerId,otpType,challenges.Count+1,expiry,now);
        db.CheckoutOtpChallenges.Add(challenge);
        db.AuditRecords.Add(AuditRecord.Create("checkout.otp.requested","CheckoutOtpChallenge",challenge.Id,null,now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(session,payment,challenge);
    }
    public async Task FinalizeOtpChallengeAsync(Guid challengeId,string? protectedReferenceCode,DateTimeOffset expiresAt,
        bool synthetic,DateTimeOffset now,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var challenge=await db.CheckoutOtpChallenges.SingleAsync(x=>x.Id==challengeId,ct);
        _=await db.CheckoutSessions.FromSqlInterpolated($"SELECT * FROM credit_payments.checkout_sessions WHERE checkout_session_id = {challenge.CheckoutSessionId} FOR UPDATE").SingleAsync(ct);
        var active=await db.CheckoutOtpChallenges.Where(x=>x.CheckoutSessionId==challenge.CheckoutSessionId&&x.Id!=challenge.Id&&
            (x.Status==CheckoutOtpChallengeStatus.Active||x.Status==CheckoutOtpChallengeStatus.Synthetic)).ToListAsync(ct);
        foreach(var item in active)item.Supersede(now);
        if(active.Count>0)await db.SaveChangesAsync(ct);
        if(synthetic)challenge.ActivateSynthetic(expiresAt,now);else challenge.Activate(protectedReferenceCode!,expiresAt,now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    public async Task MarkOtpDispatchFailedAsync(Guid challengeId,DateTimeOffset now,CancellationToken ct)
    {
        var challenge=await db.CheckoutOtpChallenges.SingleAsync(x=>x.Id==challengeId,ct);
        challenge.MarkDispatchFailed(now);
        db.AuditRecords.Add(AuditRecord.Create("checkout.otp.delivery-failed","CheckoutOtpChallenge",challenge.Id,null,now));
        await db.SaveChangesAsync(ct);
    }
    public async Task<OtpChallengeDispatch?> BeginOtpVerificationAsync(byte[] tokenHash,Guid challengeId,DateTimeOffset now,
        int maximumAttempts,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var session=await db.CheckoutSessions.FromSqlInterpolated($"SELECT * FROM credit_payments.checkout_sessions WHERE token_hash = {tokenHash} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new CheckoutStateException("checkout_not_found","نشست پرداخت یافت نشد");
        var payment=await db.GatewayPayments.SingleAsync(x=>x.Id==session.GatewayPaymentId,ct);
        EnsureCheckoutUsable(session,payment,now);
        var challenge=await db.CheckoutOtpChallenges.SingleOrDefaultAsync(x=>x.Id==challengeId&&x.CheckoutSessionId==session.Id,ct);
        if(challenge is null){await transaction.CommitAsync(ct);return null;}
        if(now>=challenge.ExpiresAt){challenge.MarkExpired(now);await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return null;}
        if(!challenge.RecordVerificationAttempt(maximumAttempts,now)){await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return null;}
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(session,payment,challenge);
    }
    public async Task RecordOtpValidationFailureAsync(Guid challengeId,int maximumAttempts,DateTimeOffset now,
        string auditOutcome,CancellationToken ct)
    {
        var challenge=await db.CheckoutOtpChallenges.SingleAsync(x=>x.Id==challengeId,ct);
        challenge.Fail(maximumAttempts,now);
        db.AuditRecords.Add(AuditRecord.Create($"checkout.otp.failed.{auditOutcome}","CheckoutOtpChallenge",challenge.Id,null,now));
        await db.SaveChangesAsync(ct);
    }
    public async Task AuthenticateCheckoutAsync(byte[] tokenHash,Guid challengeId,Guid customerId,byte[] authorizationSecretHash,
        DateTimeOffset authorizationExpiresAt,DateTimeOffset now,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var session=await db.CheckoutSessions.FromSqlInterpolated($"SELECT * FROM credit_payments.checkout_sessions WHERE token_hash = {tokenHash} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new CheckoutStateException("checkout_not_found","نشست پرداخت یافت نشد");
        var payment=await db.GatewayPayments.SingleAsync(x=>x.Id==session.GatewayPaymentId,ct);
        EnsureCheckoutUsable(session,payment,now);
        if(session.CustomerId is not null&&session.CustomerId!=customerId||payment.CustomerId is not null&&payment.CustomerId!=customerId)
            throw new CheckoutStateException("customer_switch_forbidden","مشتری نشست پرداخت قابل تغییر نیست");
        var challenge=await db.CheckoutOtpChallenges.SingleOrDefaultAsync(x=>x.Id==challengeId&&x.CheckoutSessionId==session.Id,ct)
            ?? throw new CheckoutStateException("otp_invalid_or_expired","کد تأیید نامعتبر یا منقضی شده است");
        challenge.MarkVerified(now);
        session.Authenticate(customerId,authorizationSecretHash,authorizationExpiresAt,now);
        payment.BindCustomer(customerId,now);
        db.AuditRecords.Add(AuditRecord.Create("checkout.otp.validated","CheckoutOtpChallenge",challenge.Id,customerId.ToString("D"),now));
        db.AuditRecords.Add(AuditRecord.Create("checkout.customer-bound","CheckoutSession",session.Id,customerId.ToString("D"),now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    private static void EnsureCheckoutUsable(CheckoutSession session,GatewayPayment payment,DateTimeOffset now)
    {
        if(now>=session.ExpiresAt||now>=payment.ExpiresAt)
            throw new CheckoutStateException("checkout_expired","نشست پرداخت منقضی شده است");
        if(!session.IsActive||payment.Status!=GatewayPaymentStatus.AwaitingCustomer)
            throw new CheckoutStateException("checkout_not_found","نشست پرداخت یافت نشد");
    }
    private static void EnsureFundingContext(
        CheckoutSession session,
        GatewayPayment payment,
        Guid customerId,
        Guid? orderId,
        DateTimeOffset now)
    {
        if(now>=session.ExpiresAt||now>=payment.ExpiresAt)
            throw new CheckoutStateException("payment_expired","پرداخت منقضی شده است");
        if(session.GatewayPaymentId!=payment.Id
            || session.Status is not (CheckoutSessionStatus.Authenticated or CheckoutSessionStatus.AwaitingFunding)
            || payment.Status is not (GatewayPaymentStatus.AwaitingCustomer or GatewayPaymentStatus.AwaitingFunding))
            throw new CheckoutStateException("checkout_not_found","نشست پرداخت یافت نشد");
        if(session.CustomerId!=customerId||payment.CustomerId!=customerId)
            throw new CheckoutStateException("payment_context_invalid","اطلاعات پرداخت معتبر نیست");
        if(orderId.HasValue&&payment.OrderId!=orderId)
            throw new CheckoutStateException("payment_context_invalid","اطلاعات پرداخت معتبر نیست");
    }

    public Task<PaymentTerminationRequestResult> RequestMerchantCancellationAsync(
        Guid paymentId,Guid merchantId,Guid terminalId,string idempotencyKey,byte[] requestHash,
        string? reason,DateTimeOffset now,CancellationToken ct)=>RequestCancellationAsync(
            paymentId,merchantId,terminalId,null,null,idempotencyKey,requestHash,reason,
            PaymentTerminationSource.Merchant,now,ct);

    public Task<PaymentTerminationRequestResult> RequestCustomerCancellationAsync(
        Guid checkoutSessionId,Guid gatewayPaymentId,Guid customerId,string idempotencyKey,
        byte[] requestHash,string? reason,DateTimeOffset now,CancellationToken ct)=>RequestCancellationAsync(
            gatewayPaymentId,null,null,checkoutSessionId,customerId,idempotencyKey,requestHash,reason,
            PaymentTerminationSource.Customer,now,ct);

    private async Task<PaymentTerminationRequestResult> RequestCancellationAsync(
        Guid paymentId,Guid? merchantId,Guid? terminalId,Guid? checkoutSessionId,Guid? customerId,
        string idempotencyKey,byte[] requestHash,string? reason,PaymentTerminationSource source,
        DateTimeOffset now,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        try
        {
            var payment=await db.GatewayPayments.FromSqlInterpolated(
                $"SELECT * FROM credit_payments.gateway_payments WHERE gateway_payment_id={paymentId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if(payment is null||(merchantId.HasValue&&(payment.MerchantId!=merchantId||payment.TerminalId!=terminalId)))
            {
                await transaction.CommitAsync(ct);
                return new(PaymentTerminationRequestOutcome.NotFound,null,null,null);
            }
            var checkout=await db.CheckoutSessions.FromSqlInterpolated(
                $"SELECT * FROM credit_payments.checkout_sessions WHERE gateway_payment_id={paymentId} ORDER BY created_at DESC LIMIT 1 FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if(customerId.HasValue&&(checkout is null||checkout.Id!=checkoutSessionId
                ||checkout.CustomerId!=customerId||payment.CustomerId!=customerId))
            {
                await transaction.CommitAsync(ct);
                return new(PaymentTerminationRequestOutcome.NotFound,null,null,null);
            }

            var existingIdempotency=await db.IdempotencyRecords.SingleOrDefaultAsync(x=>
                x.TerminalId==payment.TerminalId&&x.OperationType==IdempotencyOperationTypes.CancelPayment
                &&x.IdempotencyKey==idempotencyKey,ct);
            if(existingIdempotency is not null)
            {
                await transaction.CommitAsync(ct);
                if(existingIdempotency.RequestHashVersion!=1
                    ||!CryptographicOperations.FixedTimeEquals(existingIdempotency.RequestHash,requestHash))
                    return new(PaymentTerminationRequestOutcome.IdempotencyConflict,payment,checkout,null);
                var existingTermination=await db.PaymentTerminations.AsNoTracking()
                    .SingleOrDefaultAsync(x=>x.GatewayPaymentId==payment.Id,ct);
                return CurrentTerminationResult(payment,checkout,existingTermination);
            }

            var idempotency=IdempotencyRecord.Claim(payment.TerminalId,
                IdempotencyOperationTypes.CancelPayment,idempotencyKey,requestHash,now);
            db.IdempotencyRecords.Add(idempotency);
            var termination=await db.PaymentTerminations.SingleOrDefaultAsync(
                x=>x.GatewayPaymentId==payment.Id,ct);
            PaymentTerminationRequestOutcome outcome;
            Guid? recoveryAttemptId=null;
            if(payment.Status==GatewayPaymentStatus.Succeeded
                ||payment.Status is GatewayPaymentStatus.PartiallyRefunded or GatewayPaymentStatus.Refunded)
                outcome=PaymentTerminationRequestOutcome.Succeeded;
            else if(payment.Status==GatewayPaymentStatus.Canceled)
                outcome=PaymentTerminationRequestOutcome.AlreadyCanceled;
            else if(payment.Status==GatewayPaymentStatus.Expired)
                outcome=PaymentTerminationRequestOutcome.AlreadyExpired;
            else if(payment.Status==GatewayPaymentStatus.Failed)
                outcome=PaymentTerminationRequestOutcome.InvalidState;
            else
            {
                termination??=PaymentTermination.Create(payment.Id,PaymentTerminationKind.Cancel,source,reason,now);
                if(db.Entry(termination).State==EntityState.Detached)db.PaymentTerminations.Add(termination);
                var attempt=await db.PaymentAttempts.SingleOrDefaultAsync(x=>x.GatewayPaymentId==payment.Id&&
                    (x.Status==PaymentAttemptStatus.Created||x.Status==PaymentAttemptStatus.Processing
                     ||x.Status==PaymentAttemptStatus.PendingRecovery),ct);
                if(payment.Status==GatewayPaymentStatus.Processing||attempt is not null)
                {
                    termination.MarkPendingRecovery(now,now);
                    if(attempt is not null)
                    {
                        attempt.MarkPendingRecovery(now,now);
                        recoveryAttemptId=attempt.Id;
                    }
                    outcome=PaymentTerminationRequestOutcome.Processing;
                }
                else
                {
                    FinalizeLocalTermination(payment,checkout,termination,now);
                    AddTerminationWebhook(payment,termination,now);
                    outcome=PaymentTerminationRequestOutcome.Completed;
                }
                await DisableAutomaticResumeAsync(payment.Id,now,ct);
                db.AuditRecords.Add(AuditRecord.Create("gateway-payment.termination-claimed",
                    "PaymentTermination",termination.Id,customerId?.ToString("D"),now));
                db.AuditRecords.Add(AuditRecord.Create(
                    source==PaymentTerminationSource.Merchant?"gateway-payment.merchant-cancellation-requested":"gateway-payment.customer-cancellation-requested",
                    "PaymentTermination",termination.Id,customerId?.ToString("D"),now));
            }
            idempotency.Complete(payment.Id,now);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(outcome,payment,checkout,termination,recoveryAttemptId);
        }
        catch(DbUpdateException ex) when(IsUnique(ex,out var constraint)
            &&constraint=="ux_idempotency_terminal_operation_key")
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var payment=await db.GatewayPayments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==paymentId,ct);
            if(payment is null)return new(PaymentTerminationRequestOutcome.NotFound,null,null,null);
            var idem=await db.IdempotencyRecords.AsNoTracking().SingleAsync(x=>x.TerminalId==payment.TerminalId
                &&x.OperationType==IdempotencyOperationTypes.CancelPayment&&x.IdempotencyKey==idempotencyKey,ct);
            if(idem.RequestHashVersion!=1||!CryptographicOperations.FixedTimeEquals(idem.RequestHash,requestHash))
                return new(PaymentTerminationRequestOutcome.IdempotencyConflict,payment,null,null);
            var checkout=await db.CheckoutSessions.AsNoTracking().OrderByDescending(x=>x.CreatedAt)
                .FirstOrDefaultAsync(x=>x.GatewayPaymentId==paymentId,ct);
            var termination=await db.PaymentTerminations.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.GatewayPaymentId==paymentId,ct);
            return CurrentTerminationResult(payment,checkout,termination);
        }
    }

    public Task<PaymentExecutionContext?> GetTerminationContextAsync(Guid gatewayPaymentId,CancellationToken ct)=>
        GetPaymentExecutionContextAsync(gatewayPaymentId,ct);

    public async Task<PaymentExecutionContext> FinalizeTerminationAsync(
        Guid gatewayPaymentId,DateTimeOffset now,CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var payment=await LockPayment(gatewayPaymentId,ct);
        var checkout=await db.CheckoutSessions.FromSqlInterpolated(
            $"SELECT * FROM credit_payments.checkout_sessions WHERE gateway_payment_id={gatewayPaymentId} ORDER BY created_at DESC LIMIT 1 FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        var termination=await db.PaymentTerminations.SingleAsync(x=>x.GatewayPaymentId==gatewayPaymentId,ct);
        var attempt=await db.PaymentAttempts.SingleOrDefaultAsync(x=>x.GatewayPaymentId==gatewayPaymentId&&
            (x.Status==PaymentAttemptStatus.Created||x.Status==PaymentAttemptStatus.Processing
             ||x.Status==PaymentAttemptStatus.PendingRecovery),ct);
        if(payment.Status==GatewayPaymentStatus.Processing&&termination.IsUnresolved)
        {
            if(attempt is not null)attempt.MarkFailed("terminated_without_capture",now);
            FinalizeLocalTermination(payment,checkout,termination,now);
            AddTerminationWebhook(payment,termination,now);
            db.AuditRecords.Add(AuditRecord.Create(
                termination.Kind==PaymentTerminationKind.Cancel?"gateway-payment.canceled-finalized":"gateway-payment.expired-finalized",
                "PaymentTermination",termination.Id,payment.CustomerId?.ToString("D"),now));
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new(checkout!,payment,attempt,null,termination);
    }

    public async Task<PaymentExecutionContext> FinalizeCaptureWonAsync(Guid gatewayPaymentId,
        Guid paymentAttemptId,Guid? walletIntentId,Guid walletPaymentId,DateTimeOffset paidAt,
        DateTimeOffset now,CancellationToken ct)
    {
        var result=await FinalizePaymentSuccessAsync(paymentAttemptId,walletIntentId,walletPaymentId,paidAt,now,ct);
        return await GetPaymentExecutionContextAsync(gatewayPaymentId,ct)??result;
    }

    public async Task MarkTerminationPendingRecoveryAsync(Guid gatewayPaymentId,Guid paymentAttemptId,
        DateTimeOffset now,DateTimeOffset nextRetryAt,CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var termination=await db.PaymentTerminations.SingleAsync(x=>x.GatewayPaymentId==gatewayPaymentId,ct);
        var attempt=await LockPaymentAttempt(paymentAttemptId,ct);
        termination.MarkPendingRecovery(now,nextRetryAt);
        attempt.MarkPendingRecovery(now,nextRetryAt);
        db.AuditRecords.Add(AuditRecord.Create("gateway-payment.termination-pending-recovery",
            "PaymentTermination",termination.Id,null,now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RecordTerminationAuditAsync(Guid gatewayPaymentId,Guid? actorId,string action,
        DateTimeOffset now,CancellationToken ct)
    {
        db.AuditRecords.Add(AuditRecord.Create(action,"GatewayPayment",gatewayPaymentId,
            actorId?.ToString("D"),now));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ExpirationClaim>> ClaimDueExpirationsAsync(
        DateTimeOffset now,int batchSize,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var payments=await db.GatewayPayments.FromSqlInterpolated($@"SELECT * FROM credit_payments.gateway_payments gp
            WHERE gp.expires_at <= {now} AND gp.status IN (1,2,3,4)
              AND NOT EXISTS (SELECT 1 FROM credit_payments.payment_terminations pt
                              WHERE pt.gateway_payment_id=gp.gateway_payment_id)
            ORDER BY gp.expires_at,gp.created_at
            FOR UPDATE SKIP LOCKED LIMIT {batchSize}").ToListAsync(ct);
        var claims=new List<ExpirationClaim>(payments.Count);
        foreach(var payment in payments)
        {
            var checkout=await db.CheckoutSessions.OrderByDescending(x=>x.CreatedAt)
                .FirstOrDefaultAsync(x=>x.GatewayPaymentId==payment.Id,ct);
            var termination=PaymentTermination.Create(payment.Id,PaymentTerminationKind.Expire,
                PaymentTerminationSource.System,null,now);
            db.PaymentTerminations.Add(termination);
            var attempt=await db.PaymentAttempts.SingleOrDefaultAsync(x=>x.GatewayPaymentId==payment.Id&&
                (x.Status==PaymentAttemptStatus.Created||x.Status==PaymentAttemptStatus.Processing
                 ||x.Status==PaymentAttemptStatus.PendingRecovery),ct);
            Guid? recoveryAttemptId=null;
            if(payment.Status==GatewayPaymentStatus.Processing||attempt is not null)
            {
                termination.MarkPendingRecovery(now,now);
                if(attempt is not null){attempt.MarkPendingRecovery(now,now);recoveryAttemptId=attempt.Id;}
            }
            else
            {
                FinalizeLocalTermination(payment,checkout,termination,now);
                AddTerminationWebhook(payment,termination,now);
            }
            await DisableAutomaticResumeAsync(payment.Id,now,ct);
            db.AuditRecords.Add(AuditRecord.Create("gateway-payment.expiration-requested",
                "PaymentTermination",termination.Id,null,now));
            db.AuditRecords.Add(AuditRecord.Create("gateway-payment.termination-claimed",
                "PaymentTermination",termination.Id,null,now));
            claims.Add(new(payment.Id,recoveryAttemptId));
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return claims;
    }

    private async Task DisableAutomaticResumeAsync(Guid paymentId,DateTimeOffset now,CancellationToken ct)
    {
        var continuations=await db.FundingContinuations.Where(x=>x.GatewayPaymentId==paymentId).ToListAsync(ct);
        foreach(var continuation in continuations)continuation.DisableAutomaticResume(now);
    }

    private static void FinalizeLocalTermination(GatewayPayment payment,CheckoutSession? checkout,
        PaymentTermination termination,DateTimeOffset now)
    {
        if(termination.Kind==PaymentTerminationKind.Cancel)
        {
            payment.Cancel(now);
            checkout?.Cancel(now);
        }
        else
        {
            payment.Expire(now);
            checkout?.Expire(now);
        }
        termination.Complete(now);
    }

    private static PaymentTerminationRequestResult CurrentTerminationResult(GatewayPayment payment,
        CheckoutSession? checkout,PaymentTermination? termination)
    {
        var outcome=payment.Status switch
        {
            GatewayPaymentStatus.Succeeded or GatewayPaymentStatus.PartiallyRefunded or GatewayPaymentStatus.Refunded
                =>PaymentTerminationRequestOutcome.Succeeded,
            GatewayPaymentStatus.Canceled=>PaymentTerminationRequestOutcome.AlreadyCanceled,
            GatewayPaymentStatus.Expired=>PaymentTerminationRequestOutcome.AlreadyExpired,
            GatewayPaymentStatus.Processing=>PaymentTerminationRequestOutcome.Processing,
            _ when termination?.Status==PaymentTerminationStatus.Completed=>PaymentTerminationRequestOutcome.Completed,
            _=>PaymentTerminationRequestOutcome.InvalidState
        };
        return new(outcome,payment,checkout,termination);
    }

    public async Task<RefundClaimResult> ClaimRefundAsync(Guid paymentId,Guid merchantId,Guid terminalId,
        Guid credentialId,string idempotencyKey,byte[] requestHash,long amountMinor,string? merchantReference,
        string? reason,RiskReservationRequest? risk,DateTimeOffset now,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        try
        {
            var payment=await db.GatewayPayments.FromSqlInterpolated(
                $"SELECT * FROM credit_payments.gateway_payments WHERE gateway_payment_id={paymentId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if(payment is null||payment.MerchantId!=merchantId||payment.TerminalId!=terminalId)
                return new(RefundClaimOutcome.NotFound);
            var existingKey=await db.IdempotencyRecords.SingleOrDefaultAsync(x=>x.TerminalId==terminalId&&
                x.OperationType==IdempotencyOperationTypes.CreateRefund&&x.IdempotencyKey==idempotencyKey,ct);
            if(existingKey is not null)
            {
                await tx.RollbackAsync(ct); db.ChangeTracker.Clear();
                if(existingKey.RequestHashVersion!=1||!CryptographicOperations.FixedTimeEquals(existingKey.RequestHash,requestHash))
                    return new(RefundClaimOutcome.IdempotencyConflict);
                var existing=existingKey.ResourceId is null?null:await db.Refunds.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==existingKey.ResourceId,ct);
                return existing is null?throw new InvalidOperationException("Refund idempotency binding is incomplete.")
                    :new(RefundClaimOutcome.Existing,existing,await db.GatewayPayments.AsNoTracking().SingleAsync(x=>x.Id==paymentId,ct));
            }
            if(payment.Status is not (GatewayPaymentStatus.Succeeded or GatewayPaymentStatus.PartiallyRefunded))
                return new(RefundClaimOutcome.InvalidState,null,payment);
            if(payment.WalletPaymentId is null||payment.OrderId is null||payment.CapturedAmountMinor<=0)
                return new(RefundClaimOutcome.IntegrityFailure,null,payment);
            if(merchantReference is not null&&await db.Refunds.AnyAsync(x=>x.TerminalId==terminalId&&x.MerchantRefundReference==merchantReference,ct))
                return new(RefundClaimOutcome.MerchantReferenceConflict,null,payment);
            var reserved=await db.Refunds.Where(x=>x.GatewayPaymentId==paymentId&&
                (x.Status==RefundStatus.Processing||x.Status==RefundStatus.PendingRecovery||x.Status==RefundStatus.Succeeded))
                .SumAsync(x=>(long?)x.AmountMinor,ct)??0;
            if(amountMinor<=0||reserved>payment.CapturedAmountMinor-amountMinor)
                return new(RefundClaimOutcome.AmountExceeded,null,payment);
            var refund=Refund.Claim(paymentId,terminalId,amountMinor,payment.Currency,merchantReference,reason,now);
            if(risk is not null)
            {
                risk=risk with { ResourceId=refund.Id };
                var riskResult=await ReserveRiskAsync(risk,now,ct);
                if(!riskResult.Allowed)
                {
                    await tx.CommitAsync(ct);
                    return new(RefundClaimOutcome.RiskDenied,null,payment,riskResult.RuleCode);
                }
            }
            var idem=IdempotencyRecord.Claim(terminalId,IdempotencyOperationTypes.CreateRefund,idempotencyKey,requestHash,now);
            idem.Complete(refund.Id,now);
            db.Refunds.Add(refund); db.IdempotencyRecords.Add(idem);
            db.AuditRecords.AddRange(
                AuditRecord.Create("refund.requested","Refund",refund.Id,credentialId.ToString("D"),now),
                AuditRecord.Create("refund.capacity-reserved","Refund",refund.Id,credentialId.ToString("D"),now));
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return new(RefundClaimOutcome.Claimed,refund,payment);
        }
        catch(DbUpdateException ex) when(IsUnique(ex,out var constraint)&&
            constraint is "ux_refunds_terminal_merchant_reference" or "ux_idempotency_terminal_operation_key")
        {
            await tx.RollbackAsync(ct); db.ChangeTracker.Clear();
            if(constraint=="ux_refunds_terminal_merchant_reference") return new(RefundClaimOutcome.MerchantReferenceConflict);
            var existing=await db.IdempotencyRecords.AsNoTracking().SingleAsync(x=>x.TerminalId==terminalId&&
                x.OperationType==IdempotencyOperationTypes.CreateRefund&&x.IdempotencyKey==idempotencyKey,ct);
            if(!CryptographicOperations.FixedTimeEquals(existing.RequestHash,requestHash)) return new(RefundClaimOutcome.IdempotencyConflict);
            var refund=await db.Refunds.AsNoTracking().SingleAsync(x=>x.Id==existing.ResourceId,ct);
            var payment=await db.GatewayPayments.AsNoTracking().SingleAsync(x=>x.Id==refund.GatewayPaymentId,ct);
            return new(RefundClaimOutcome.Existing,refund,payment);
        }
    }
    public Task<RefundClaimResult> ClaimRefundAsync(Guid paymentId,Guid merchantId,Guid terminalId,
        Guid credentialId,string idempotencyKey,byte[] requestHash,long amountMinor,string? merchantReference,
        string? reason,DateTimeOffset now,CancellationToken ct)=>ClaimRefundAsync(paymentId,merchantId,terminalId,
            credentialId,idempotencyKey,requestHash,amountMinor,merchantReference,reason,null,now,ct);

    public async Task<Refund?> GetRefundAsync(Guid refundId,Guid merchantId,Guid terminalId,CancellationToken ct)
    {
        var query=from r in db.Refunds.AsNoTracking() join p in db.GatewayPayments.AsNoTracking()
            on r.GatewayPaymentId equals p.Id where r.Id==refundId&&p.MerchantId==merchantId&&p.TerminalId==terminalId select r;
        return await query.SingleOrDefaultAsync(ct);
    }
    public async Task<IReadOnlyList<Refund>> ListRefundsAsync(Guid paymentId,Guid merchantId,Guid terminalId,CancellationToken ct)
    {
        if(!await db.GatewayPayments.AsNoTracking().AnyAsync(x=>x.Id==paymentId&&x.MerchantId==merchantId&&x.TerminalId==terminalId,ct)) return [];
        return await db.Refunds.AsNoTracking().Where(x=>x.GatewayPaymentId==paymentId).OrderByDescending(x=>x.CreatedAt).ToListAsync(ct);
    }
    public async Task<RefundContext?> GetRefundContextAsync(Guid refundId,CancellationToken ct)
    {
        var refund=await db.Refunds.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==refundId,ct);
        if(refund is null)return null;
        var payment=await db.GatewayPayments.AsNoTracking().SingleAsync(x=>x.Id==refund.GatewayPaymentId,ct);
        return new(refund,payment);
    }
    public async Task<RefundContext> FinalizeRefundSuccessAsync(Guid refundId,Guid walletRefundId,
        DateTimeOffset walletCompletedAt,DateTimeOffset now,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var refund=await db.Refunds.FromSqlInterpolated($"SELECT * FROM credit_payments.refunds WHERE refund_id={refundId} FOR UPDATE").SingleAsync(ct);
        var payment=await db.GatewayPayments.FromSqlInterpolated($"SELECT * FROM credit_payments.gateway_payments WHERE gateway_payment_id={refund.GatewayPaymentId} FOR UPDATE").SingleAsync(ct);
        if(refund.Status!=RefundStatus.Succeeded)
        {
            payment.ApplySuccessfulRefund(refund.AmountMinor,now);
            refund.MarkSucceeded(walletRefundId,payment.Status==GatewayPaymentStatus.Refunded,walletCompletedAt,now);
            WebhookEventFactory.AddRefund(db,refund,payment,WebhookEventTypes.RefundSucceeded,
                walletCompletedAt,now);
            var merchant=await db.Merchants.SingleAsync(x=>x.Id==payment.MerchantId,ct);
            var integrationEvent=SettlementIntegrationEventFactory.Refunded(refund,payment,merchant.SupplierId,now);
            db.IntegrationEvents.Add(integrationEvent);
            db.AuditRecords.Add(AuditRecord.Create("settlement-integration.fact-created",
                "IntegrationEvent",integrationEvent.Id,null,now));
            db.AuditRecords.Add(AuditRecord.Create(payment.Status==GatewayPaymentStatus.Refunded?"gateway-payment.fully-refunded":"gateway-payment.partially-refunded","GatewayPayment",payment.Id,null,now));
            db.AuditRecords.Add(AuditRecord.Create("wallet-refund.succeeded","Refund",refund.Id,null,now));
            if(refund.RetryCount>0)db.AuditRecords.Add(AuditRecord.Create("refund.recovery-converged","Refund",refund.Id,null,now));
            await db.SaveChangesAsync(ct);
        }
        else if(refund.WalletRefundId!=walletRefundId) throw new InvalidOperationException("Refund wallet reference mismatch.");
        await tx.CommitAsync(ct); return new(refund,payment);
    }
    public async Task MarkRefundFailedAsync(Guid refundId,string code,DateTimeOffset now,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var refund=await db.Refunds.FromSqlInterpolated(
            $"SELECT * FROM credit_payments.refunds WHERE refund_id={refundId} FOR UPDATE").SingleAsync(ct);
        if(refund.Status==RefundStatus.Failed){await tx.CommitAsync(ct);return;}
        var payment=await db.GatewayPayments.SingleAsync(x=>x.Id==refund.GatewayPaymentId,ct);
        refund.MarkFailed(code,now);
        WebhookEventFactory.AddRefund(db,refund,payment,WebhookEventTypes.RefundFailed,now,now);
        await ReleaseRiskAsync(RiskScopeType.Terminal,refund.TerminalId,RiskOperationTypes.RefundCreate,refund.Id,now,ct);
        db.AuditRecords.Add(AuditRecord.Create("wallet-refund.failed","Refund",refund.Id,null,now));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task MarkRefundPendingRecoveryAsync(Guid refundId,DateTimeOffset now,DateTimeOffset nextRetryAt,CancellationToken ct)
    {
        var refund=await db.Refunds.SingleAsync(x=>x.Id==refundId,ct); if(refund.Status is RefundStatus.Succeeded or RefundStatus.Failed)return;
        refund.MarkPendingRecovery(now,nextRetryAt); db.AuditRecords.Add(AuditRecord.Create("refund.pending-recovery","Refund",refund.Id,null,now)); await db.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<Guid>> LeaseRefundRecoveryBatchAsync(string owner,DateTimeOffset now,DateTimeOffset until,int size,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var rows=await db.Refunds.FromSqlInterpolated($@"SELECT * FROM credit_payments.refunds WHERE
            (((status IN (2,3)) AND (next_retry_at IS NULL OR next_retry_at<={now})) OR
             (status=4 AND order_projection_status=2 AND (order_projection_next_retry_at IS NULL OR order_projection_next_retry_at<={now})))
            AND (lease_until IS NULL OR lease_until<={now}) ORDER BY created_at FOR UPDATE SKIP LOCKED LIMIT {size}").ToListAsync(ct);
        foreach(var row in rows)row.ClaimLease(owner,until,now);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return rows.Select(x=>x.Id).ToArray();
    }
    public async Task CompleteOrderRefundProjectionAsync(Guid refundId,DateTimeOffset now,CancellationToken ct)
    {var r=await db.Refunds.SingleAsync(x=>x.Id==refundId,ct);if(r.OrderProjectionStatus==OrderRefundProjectionStatus.Completed)return;r.CompleteOrderProjection(now);db.AuditRecords.Add(AuditRecord.Create("order-refund-projection.succeeded","Refund",r.Id,null,now));await db.SaveChangesAsync(ct);}
    public async Task ScheduleOrderRefundProjectionAsync(Guid refundId,DateTimeOffset now,DateTimeOffset next,CancellationToken ct)
    {var r=await db.Refunds.SingleAsync(x=>x.Id==refundId,ct);r.ScheduleOrderProjection(now,next);db.AuditRecords.Add(AuditRecord.Create("order-refund-projection.pending","Refund",r.Id,null,now));await db.SaveChangesAsync(ct);}
    public async Task RecordRefundAuditAsync(Guid refundId,string action,DateTimeOffset now,CancellationToken ct)
    {db.AuditRecords.Add(AuditRecord.Create(action,"Refund",refundId,null,now));await db.SaveChangesAsync(ct);}
    public async Task<bool> ResendDeadLetterAsync(Guid eventId,string? actorId,string? reason,
        DateTimeOffset now,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var delivery=await db.WebhookDeliveries.FromSqlInterpolated(
            $"SELECT * FROM credit_payments.webhook_deliveries WHERE webhook_event_id={eventId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if(delivery is null||delivery.Status!=WebhookDeliveryStatus.DeadLettered)
        {await tx.CommitAsync(ct);return false;}
        delivery.ManualResend(now);
        db.AuditRecords.Add(AuditRecord.Create("webhook-delivery.manual-resend",
            "WebhookDelivery",delivery.Id,actorId,now));
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
    public void Add(Merchant merchant)=>db.Merchants.Add(merchant);
    public void AddAudit(AuditRecord audit)=>db.AuditRecords.Add(audit);
    public Task SaveChangesAsync(CancellationToken ct)=>db.SaveChangesAsync(ct);
    public async Task RecordAuthenticationFailureAsync(string action,Guid? credentialId,Guid? terminalId,
        DateTimeOffset now,CancellationToken ct)
    {
        db.AuditRecords.Add(AuditRecord.Create(action,"MerchantAuthentication",credentialId??Guid.Empty,null,now));
        await db.SaveChangesAsync(ct);
    }
    private static IQueryable<T> Query<T>(DbSet<T> set,bool tracking) where T:class=>tracking?set:set.AsNoTracking();
    private sealed record RiskReservationResult(bool Allowed,string? RuleCode=null);

    private async Task<RiskReservationResult> ReserveRiskAsync(
        RiskReservationRequest request,DateTimeOffset now,CancellationToken ct)
    {
        var existing=await db.RiskReservations.SingleOrDefaultAsync(x=>
            x.ScopeType==request.ScopeType&&x.ScopeId==request.ScopeId&&
            x.OperationType==request.OperationType&&x.ResourceId==request.ResourceId,ct);
        if(existing is { ReleasedAt:null })return new(true);

        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO credit_payments.risk_velocity_counters
                (risk_velocity_counter_id,scope_type,scope_id,operation_type,bucket_start,
                 accepted_count,accepted_amount_minor,updated_at,version)
            VALUES ({Guid.NewGuid()},{(short)request.ScopeType},{request.ScopeId},{request.OperationType},
                    {request.BucketStart},0,0,{now},1)
            ON CONFLICT (scope_type,scope_id,operation_type,bucket_start) DO NOTHING",ct);
        var counter=await db.RiskVelocityCounters.FromSqlInterpolated($@"
            SELECT * FROM credit_payments.risk_velocity_counters
            WHERE scope_type={(short)request.ScopeType} AND scope_id={request.ScopeId}
              AND operation_type={request.OperationType} AND bucket_start={request.BucketStart}
            FOR UPDATE").SingleAsync(ct);

        string? deniedRule=null; long? current=null; long? limit=null;
        if(request.CountLimit is { } countLimit&&counter.AcceptedCount>=countLimit)
        {deniedRule=request.CountRuleCode;current=counter.AcceptedCount;limit=countLimit;}
        else if(request.AmountLimitMinor is { } amountLimit&&
                counter.AcceptedAmountMinor>amountLimit-request.AmountMinor)
        {deniedRule=request.AmountRuleCode;current=counter.AcceptedAmountMinor;limit=amountLimit;}
        if(deniedRule is not null)
        {
            db.RiskDecisions.Add(RiskDecisionRecord.Create(request.OperationType,request.MerchantId,
                request.TerminalId,request.CustomerId,request.ResourceId,deniedRule,RiskDecision.Denied,
                current,limit,now));
            var action=request.OperationType switch
            {
                RiskOperationTypes.RefundCreate=>"risk_refund_limit_denied",
                RiskOperationTypes.FundingStart=>"funding_attempt_limit_denied",
                _ when request.ScopeType==RiskScopeType.Customer=>"risk_customer_limit_denied",
                _=>"risk_payment_limit_denied"
            };
            db.AuditRecords.Add(AuditRecord.Create(action,"RiskDecision",request.ResourceId,
                request.CustomerId?.ToString("D"),now));
            await db.SaveChangesAsync(ct);
            return new(false,deniedRule);
        }

        counter.Reserve(request.AmountMinor,now);
        if(existing is null)
            db.RiskReservations.Add(RiskReservation.Create(counter.Id,request.ScopeType,request.ScopeId,
                request.OperationType,request.ResourceId,request.AmountMinor,request.BucketStart,now));
        else existing.Reactivate(counter.Id,request.AmountMinor,request.BucketStart,now);
        if(request.CountLimit is not null)
            db.RiskDecisions.Add(RiskDecisionRecord.Create(request.OperationType,request.MerchantId,
                request.TerminalId,request.CustomerId,request.ResourceId,request.CountRuleCode,RiskDecision.Allowed,
                counter.AcceptedCount,request.CountLimit,now));
        if(request.AmountLimitMinor is not null)
            db.RiskDecisions.Add(RiskDecisionRecord.Create(request.OperationType,request.MerchantId,
                request.TerminalId,request.CustomerId,request.ResourceId,request.AmountRuleCode,RiskDecision.Allowed,
                counter.AcceptedAmountMinor,request.AmountLimitMinor,now));
        await db.SaveChangesAsync(ct);
        return new(true);
    }

    private async Task ReleaseRiskAsync(RiskScopeType scopeType,Guid scopeId,string operationType,
        Guid resourceId,DateTimeOffset now,CancellationToken ct)
    {
        var reservation=await db.RiskReservations.FromSqlInterpolated($@"
            SELECT * FROM credit_payments.risk_reservations
            WHERE scope_type={(short)scopeType} AND scope_id={scopeId} AND operation_type={operationType}
              AND resource_id={resourceId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if(reservation is null||reservation.ReleasedAt is not null)return;
        var counter=await db.RiskVelocityCounters.FromSqlInterpolated($@"
            SELECT * FROM credit_payments.risk_velocity_counters
            WHERE risk_velocity_counter_id={reservation.CounterId} FOR UPDATE").SingleAsync(ct);
        counter.Release(reservation.AmountMinor,now); reservation.Release(now);
    }
    private static bool IsUnique(DbUpdateException exception,out string? constraint)
    {
        if(exception.InnerException is PostgresException {SqlState:PostgresErrorCodes.UniqueViolation} postgres){constraint=postgres.ConstraintName;return true;}
        constraint=null;return false;
    }

    private void AddTerminationWebhook(GatewayPayment payment,PaymentTermination termination,
        DateTimeOffset now)
    {
        var eventType=termination.Kind==PaymentTerminationKind.Cancel
            ?WebhookEventTypes.PaymentCanceled:WebhookEventTypes.PaymentExpired;
        WebhookEventFactory.AddPayment(db,payment,eventType,termination.ResolvedAt??now,now);
    }
}

public sealed class Pbkdf2CredentialSecretService:ICredentialSecretService
{
    private const int Iterations=210_000; private const int Version=1; private const string Algorithm="PBKDF2-SHA256";
    public (string ClientId,string Secret,string Hash,string Algorithm,int Version) Generate(){var client="cpg_"+Base64Url(RandomNumberGenerator.GetBytes(24));var secret=Base64Url(RandomNumberGenerator.GetBytes(32));var salt=RandomNumberGenerator.GetBytes(16);var derived=Rfc2898DeriveBytes.Pbkdf2(secret,salt,Iterations,HashAlgorithmName.SHA256,32);return(client,secret,$"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(derived)}",Algorithm,Version);}
    public bool Verify(string secret,string hash,string algorithm,int version){try{if(algorithm!=Algorithm||version!=Version)return false;var p=hash.Split('.');if(p.Length!=3||!int.TryParse(p[0],out var iterations))return false;var salt=Convert.FromBase64String(p[1]);var expected=Convert.FromBase64String(p[2]);var actual=Rfc2898DeriveBytes.Pbkdf2(secret,salt,iterations,HashAlgorithmName.SHA256,expected.Length);return CryptographicOperations.FixedTimeEquals(actual,expected);}catch(FormatException){return false;}}
    private static string Base64Url(byte[] bytes)=>Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
}

public sealed class DataProtectionWebhookSecretProtector(IDataProtectionProvider provider):IWebhookSecretProtector
{
    private readonly IDataProtector protector=provider.CreateProtector("Refahi.CreditPayments.WebhookSigningSecret.v1");
    public (string Secret,string Ciphertext) GenerateAndProtect(){var secret=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');return(secret,protector.Protect(secret));}
    public bool TryUnprotect(string ciphertext,out string secret)
    {
        try{secret=protector.Unprotect(ciphertext);return true;}
        catch(CryptographicException){secret=string.Empty;return false;}
    }
}
