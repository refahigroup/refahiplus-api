using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;

namespace Refahi.Modules.CreditPayments.Application;

public sealed class ConfirmCheckoutPaymentHandler(
    IPaymentExecutionRepository executions,
    ICheckoutSecurityService security,
    IMediator mediator,
    TimeProvider clock)
    : IRequestHandler<ConfirmCheckoutPaymentCommand, CheckoutPaymentExecutionDto>
{
    public async Task<CheckoutPaymentExecutionDto> Handle(
        ConfirmCheckoutPaymentCommand request,
        CancellationToken ct)
    {
        if (!security.TryHashPresentedToken(request.Token, out var tokenHash))
            throw Error("checkout_not_found", "نشست پرداخت یافت نشد", 404);
        var context = await executions.GetPaymentExecutionContextAsync(tokenHash, ct)
            ?? throw Error("checkout_not_found", "نشست پرداخت یافت نشد", 404);
        var now = clock.GetUtcNow();
        if (context.Payment.Status == GatewayPaymentStatus.Succeeded)
            return PaymentExecutionSupport.Dto(context.Payment);
        if (context.Payment.Status == GatewayPaymentStatus.Processing)
            return PaymentExecutionSupport.Dto(context.Payment);
        if (now >= context.Payment.ExpiresAt || now >= context.Session.ExpiresAt)
            throw Error("payment_expired", "پرداخت منقضی شده است", 410);
        if (context.Payment.CustomerId is null || context.Session.CustomerId != context.Payment.CustomerId)
            throw Error("payment_context_invalid", "اطلاعات پرداخت معتبر نیست", 503);
        if (string.IsNullOrWhiteSpace(request.CheckoutAuthorizationSecret)
            || context.Session.AuthorizationSecretHash is null
            || context.Session.AuthorizationExpiresAt <= now
            || !security.VerifyAuthorizationSecret(
                request.CheckoutAuthorizationSecret,
                context.Session.AuthorizationSecretHash))
            throw Error("checkout_authentication_required", "احراز هویت نشست پرداخت الزامی است", 401);

        return await mediator.Send(
            new ExecuteGatewayPaymentCommand(context.Payment.Id, "CustomerConfirmation"), ct);
    }

    private static CreditPaymentsApplicationException Error(string code,string message,int status) =>
        new(code,message,status);
}

public sealed class ExecuteGatewayPaymentHandler(
    IPaymentExecutionRepository executions,
    ICheckoutRepository checkouts,
    IMerchantPaymentPolicyResolver policies,
    IMediator mediator,
    TimeProvider clock,
    IOptions<PaymentExecutionOptions> options,
    IOptions<RiskOptions> riskOptions,
    ILogger<ExecuteGatewayPaymentHandler> logger)
    : IRequestHandler<ExecuteGatewayPaymentCommand, CheckoutPaymentExecutionDto>
{
    public ExecuteGatewayPaymentHandler(IPaymentExecutionRepository executions,ICheckoutRepository checkouts,
        IMerchantPaymentPolicyResolver policies,IMediator mediator,TimeProvider clock,
        IOptions<PaymentExecutionOptions> options,ILogger<ExecuteGatewayPaymentHandler> logger)
        :this(executions,checkouts,policies,mediator,clock,options,
            Microsoft.Extensions.Options.Options.Create(new RiskOptions()),logger) { }

    public async Task<CheckoutPaymentExecutionDto> Handle(
        ExecuteGatewayPaymentCommand request,
        CancellationToken ct)
    {
        var context = await executions.GetPaymentExecutionContextAsync(request.GatewayPaymentId, ct)
            ?? throw new CreditPaymentsApplicationException("payment_not_found", "پرداخت یافت نشد", 404);
        if (context.Payment.Status == GatewayPaymentStatus.Succeeded)
            return PaymentExecutionSupport.Dto(context.Payment);
        if (context.Payment.Status == GatewayPaymentStatus.Processing
            || context.Session.Status == CheckoutSessionStatus.Processing
            || context.ActiveAttempt is not null)
            return new("processing",context.Payment.AmountMinor,context.Payment.Currency,null,null);

        var trigger = ParseTrigger(request.Trigger);
        ValidateReadyContext(context, trigger, clock.GetUtcNow());
        var payment = context.Payment;
        var customerId = payment.CustomerId!.Value;
        var orderId = payment.OrderId!.Value;

        var order = await mediator.Send(
            new GetOrderPaymentExecutionStatusQuery(orderId, customerId), ct);
        if (!PaymentExecutionSupport.IsOrderConsistent(order, payment))
        {
            logger.LogCritical(
                "Order execution integrity mismatch. GatewayPaymentId={GatewayPaymentId} OrderId={OrderId}",
                payment.Id, orderId);
            await checkouts.RecordFundingIntegrityFailureAsync(payment.Id, customerId, clock.GetUtcNow(), ct);
            throw new CreditPaymentsApplicationException("payment_context_invalid", "اطلاعات پرداخت معتبر نیست", 503);
        }

        OrderFundingEligibilityDto eligibility;
        try
        {
            eligibility = await mediator.Send(new GetOrderFundingEligibilityQuery(orderId, customerId), ct)
                ?? throw new InvalidOperationException("Order eligibility unavailable.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex,"Pre-execution funding inquiry failed. GatewayPaymentId={GatewayPaymentId}",payment.Id);
            throw new CreditPaymentsApplicationException("order_temporarily_unavailable", "بررسی پرداخت موقتاً ممکن نیست",503);
        }
        if (!FundingContinuationSupport.IsEligibilityConsistent(eligibility,payment,orderId,customerId))
            throw new CreditPaymentsApplicationException("payment_context_invalid", "اطلاعات پرداخت معتبر نیست",503);
        if (!eligibility.IsSufficient)
        {
            var updated=await checkouts.ApplyFundingEvaluationAsync(context.Session.Id,payment.Id,customerId,
                orderId,false,eligibility.TotalEligibleSpendableAmountMinor,eligibility.DeficitAmountMinor,
                eligibility.EvaluatedAt,ct);
            return PaymentExecutionSupport.Dto(updated.Payment,new CheckoutFundingDto(false,
                eligibility.TotalEligibleSpendableAmountMinor,eligibility.DeficitAmountMinor,
                eligibility.HasPermittedPersonalWallet,eligibility.EvaluatedAt),"insufficient_funds");
        }

        MerchantPaymentPolicy? policy;
        try
        {
            policy=await policies.ResolveAsync(payment.MerchantId,payment.TerminalId,
                payment.ApprovedCategoryCodeSnapshot,payment.AmountMinor,payment.Currency,ct);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception ex)
        {
            logger.LogError(ex,"Payment policy resolution failed. GatewayPaymentId={GatewayPaymentId}",payment.Id);
            policy=null;
        }
        if(policy is null||policy.DestinationWalletId==Guid.Empty
            ||policy.CategoryCode!=payment.ApprovedCategoryCodeSnapshot
            ||policy.EffectiveAt>clock.GetUtcNow()||policy.PolicyHash.Length!=64)
        {
            logger.LogError("Payment policy unavailable. GatewayPaymentId={GatewayPaymentId} MerchantId={MerchantId} TerminalId={TerminalId}",
                payment.Id,payment.MerchantId,payment.TerminalId);
            return PaymentExecutionSupport.Dto(payment,null,"payment_policy_unavailable");
        }

        PaymentExecutionClaimResult claim;
        var risk=RiskPolicies.CustomerPayment(riskOptions.Value,payment.Id,payment.MerchantId,
            payment.TerminalId,customerId,payment.AmountMinor,clock.GetUtcNow());
        try
        {
            claim=await executions.ClaimPaymentExecutionAsync(payment.Id,trigger,policy.PolicyId,
                policy.PolicyVersion,policy.PolicyHash,policy.DestinationWalletId,risk,clock.GetUtcNow(),ct);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception ex) when(risk is not null)
        {
            logger.LogError(ex,"Customer payment risk decision unavailable. GatewayPaymentId={GatewayPaymentId}",payment.Id);
            throw new CreditPaymentsApplicationException("risk_temporarily_unavailable",
                "بررسی محدودیت پرداخت موقتاً ممکن نیست",503);
        }
        if(claim.Outcome==PaymentExecutionClaimOutcome.RiskDenied)
            throw new CreditPaymentsApplicationException("customer_payment_limit_exceeded",
                "محدودیت روزانه پرداخت مشتری عبور کرده است",429);
        if(claim.Outcome!=PaymentExecutionClaimOutcome.Claimed)
            return PaymentExecutionSupport.Dto(claim.Context.Payment);
        var attempt=claim.Context.ActiveAttempt!;
        try
        {
            logger.LogInformation("Orders payment execution requested. GatewayPaymentId={GatewayPaymentId} PaymentAttemptId={PaymentAttemptId} OrderId={OrderId}",
                payment.Id,attempt.Id,orderId);
            var result=await mediator.Send(new ExecuteOrderPaymentCommand(orderId,customerId,
                $"cpg-pay-{payment.Id:N}",attempt.DestinationWalletId),ct);
            if(result.ExecutionStatus=="InsufficientFunds")
            {
                var insufficient=await executions.FinalizePaymentInsufficientAsync(attempt.Id,clock.GetUtcNow(),ct);
                return PaymentExecutionSupport.Dto(insufficient.Payment,null,"insufficient_funds");
            }
            if(result.ExecutionStatus!="Succeeded"||result.PaymentId is null)
                throw new InvalidOperationException("Orders returned an unknown payment execution outcome.");
            var authoritative=await mediator.Send(new GetOrderPaymentExecutionStatusQuery(orderId,customerId),ct);
            if(authoritative?.ExecutionStatus!="Succeeded"||authoritative.PaymentId!=result.PaymentId)
                throw new InvalidOperationException("Orders payment success could not be authoritatively confirmed.");
            var succeeded=await executions.FinalizePaymentSuccessAsync(attempt.Id,authoritative.PaymentIntentId,
                authoritative.PaymentId.Value,authoritative.PaidAt??clock.GetUtcNow(),clock.GetUtcNow(),ct);
            return PaymentExecutionSupport.Dto(succeeded.Payment);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested)
        {
            await executions.MarkPaymentPendingRecoveryAsync(attempt.Id,clock.GetUtcNow(),
                NextRetry(clock.GetUtcNow(),attempt.RetryCount,options.Value),CancellationToken.None);
            throw;
        }
        catch(Exception ex)
        {
            logger.LogWarning(ex,"Orders payment outcome unknown. GatewayPaymentId={GatewayPaymentId} PaymentAttemptId={PaymentAttemptId}",
                payment.Id,attempt.Id);
            var now=clock.GetUtcNow();
            await executions.MarkPaymentPendingRecoveryAsync(attempt.Id,now,NextRetry(now,attempt.RetryCount,options.Value),ct);
            return new("processing",payment.AmountMinor,payment.Currency,null,null,null,"unknown");
        }
    }

    private static PaymentAttemptTrigger ParseTrigger(string trigger)=>trigger switch
    {
        "CustomerConfirmation"=>PaymentAttemptTrigger.CustomerConfirmation,
        "FundingAutoResume"=>PaymentAttemptTrigger.FundingAutoResume,
        "Recovery"=>PaymentAttemptTrigger.Recovery,
        _=>throw new CreditPaymentsApplicationException("invalid_execution_trigger","نوع اجرای پرداخت معتبر نیست")
    };
    private static void ValidateReadyContext(PaymentExecutionContext context,PaymentAttemptTrigger trigger,DateTimeOffset now)
    {
        var p=context.Payment;var s=context.Session;
        if(now>=p.ExpiresAt||now>=s.ExpiresAt||p.Status!=GatewayPaymentStatus.AwaitingCustomer
            ||s.Status!=CheckoutSessionStatus.Authenticated||p.CustomerId is null||p.OrderId is null
            ||s.CustomerId!=p.CustomerId)
            throw new CreditPaymentsApplicationException("payment_context_invalid","اطلاعات پرداخت معتبر نیست",409);
        if(trigger==PaymentAttemptTrigger.FundingAutoResume)
        {
            var c=context.LatestFundingContinuation;
            if(c is null||c.Status!=FundingContinuationStatus.Succeeded
                ||c.GatewayPaymentId!=p.Id||c.CheckoutSessionId!=s.Id||c.CustomerId!=p.CustomerId)
                throw new CreditPaymentsApplicationException("auto_resume_not_ready","پرداخت خودکار آماده نیست",409);
        }
    }
    internal static DateTimeOffset NextRetry(DateTimeOffset now,int retryCount,PaymentExecutionOptions configured)
    {
        var seconds=Math.Min(Math.Max(1,configured.MaximumRetrySeconds),
            Math.Max(1,configured.BaseRetrySeconds)*(1<<Math.Min(10,Math.Max(0,retryCount))));
        return now.AddSeconds(seconds+Random.Shared.Next(0,Math.Max(2,seconds/5)));
    }
}

public sealed class RecoverGatewayPaymentHandler(
    IPaymentExecutionRepository executions,
    IMediator mediator,
    TimeProvider clock,
    IOptions<PaymentExecutionOptions> options,
    ILogger<RecoverGatewayPaymentHandler> logger)
    : IRequestHandler<RecoverGatewayPaymentCommand,Unit>
{
    public async Task<Unit> Handle(RecoverGatewayPaymentCommand request,CancellationToken ct)
    {
        var context=await executions.GetPaymentAttemptContextAsync(request.PaymentAttemptId,ct);
        if(context?.ActiveAttempt is not { } attempt||context.Payment.Status!=GatewayPaymentStatus.Processing)
            return Unit.Value;
        if(context.Termination is { IsUnresolved:true })
        {
            await mediator.Send(new ResolvePaymentTerminationCommand(context.Payment.Id),ct);
            return Unit.Value;
        }
        try
        {
            var status=await mediator.Send(new GetOrderPaymentExecutionStatusQuery(attempt.OrderId,
                context.Payment.CustomerId!.Value),ct);
            if(!PaymentExecutionSupport.IsOrderConsistent(status,context.Payment))
                throw new InvalidOperationException("Orders recovery context mismatch.");
            if(status!.ExecutionStatus=="Succeeded"&&status.PaymentId is { } paymentId)
            {
                await executions.FinalizePaymentSuccessAsync(attempt.Id,status.PaymentIntentId,paymentId,
                    status.PaidAt??clock.GetUtcNow(),clock.GetUtcNow(),ct);
                await executions.RecordPaymentExecutionAuditAsync(context.Payment.Id,context.Payment.CustomerId,
                    "gateway-payment.recovery-converged",clock.GetUtcNow(),ct);
                return Unit.Value;
            }
            if(status.ExecutionStatus=="NoCapture")
            {
                await executions.FinalizePaymentNoEffectAsync(attempt.Id,"financial_rejected",clock.GetUtcNow(),ct);
                return Unit.Value;
            }
            if(status.ExecutionStatus=="NotStarted")
            {
                await executions.RecordPaymentExecutionAuditAsync(context.Payment.Id,context.Payment.CustomerId,
                    "gateway-payment.recovery-replay-requested",clock.GetUtcNow(),ct);
                var result=await mediator.Send(new ExecuteOrderPaymentCommand(attempt.OrderId,
                    context.Payment.CustomerId.Value,$"cpg-pay-{context.Payment.Id:N}",attempt.DestinationWalletId),ct);
                if(result.ExecutionStatus=="InsufficientFunds")
                    await executions.FinalizePaymentInsufficientAsync(attempt.Id,clock.GetUtcNow(),ct);
                else if(result.ExecutionStatus=="Succeeded"&&result.PaymentId is { } recoveredPaymentId)
                {
                    var authoritative=await mediator.Send(new GetOrderPaymentExecutionStatusQuery(attempt.OrderId,
                        context.Payment.CustomerId.Value),ct);
                    if(authoritative?.ExecutionStatus!="Succeeded"||authoritative.PaymentId!=recoveredPaymentId)
                        throw new InvalidOperationException("Recovered payment was not authoritative.");
                    await executions.FinalizePaymentSuccessAsync(attempt.Id,authoritative.PaymentIntentId,
                        recoveredPaymentId,authoritative.PaidAt??clock.GetUtcNow(),clock.GetUtcNow(),ct);
                    await executions.RecordPaymentExecutionAuditAsync(context.Payment.Id,context.Payment.CustomerId,
                        "gateway-payment.recovery-converged",clock.GetUtcNow(),ct);
                }
                else throw new InvalidOperationException("Recovered execution outcome is unknown.");
                return Unit.Value;
            }
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception ex){logger.LogWarning(ex,"Payment recovery remains pending. PaymentAttemptId={PaymentAttemptId}",attempt.Id);}
        var now=clock.GetUtcNow();
        await executions.MarkPaymentPendingRecoveryAsync(attempt.Id,now,
            ExecuteGatewayPaymentHandler.NextRetry(now,attempt.RetryCount,options.Value),ct);
        return Unit.Value;
    }
}

internal static class PaymentExecutionSupport
{
    public static bool IsOrderConsistent(OrderPaymentExecutionStatusDto? order,GatewayPayment payment)=>
        order is not null&&payment.OrderId==order.OrderId&&payment.CustomerId==order.UserId
        &&payment.AmountMinor==order.AmountMinor&&order.Currency=="IRR"
        &&order.SourceModule=="CreditPayments"&&order.ReferenceType=="CreditPayments"
        &&order.SourceReferenceId==payment.Id&&order.CategoryCode==payment.ApprovedCategoryCodeSnapshot;

    public static CheckoutPaymentExecutionDto Dto(GatewayPayment payment,CheckoutFundingDto? funding=null,string? outcome=null)=>
        new(CreateGatewayPaymentHandler.PaymentStatusName(payment.Status),payment.AmountMinor,payment.Currency,
            payment.PaidAt,payment.Status==GatewayPaymentStatus.Succeeded?payment.ReturnUrl:null,funding,outcome);
}
