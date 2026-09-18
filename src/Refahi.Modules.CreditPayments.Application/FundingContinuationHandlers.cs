using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.PaymentGateway.Application.Contracts.Funding;

namespace Refahi.Modules.CreditPayments.Application;

public sealed class StartCheckoutFundingHandler(
    ICheckoutRepository checkouts,
    IFundingContinuationRepository continuations,
    ICheckoutSecurityService security,
    IMediator mediator,
    IWalletFundingService walletFunding,
    TimeProvider clock,
    IOptions<CheckoutOptions> checkoutOptions,
    IOptions<FundingContinuationOptions> options,
    IOptions<RiskOptions> riskOptions,
    ILogger<StartCheckoutFundingHandler> logger)
    : IRequestHandler<StartCheckoutFundingCommand, CheckoutFundingContinuationDto>
{
    public StartCheckoutFundingHandler(ICheckoutRepository checkouts,
        IFundingContinuationRepository continuations,ICheckoutSecurityService security,IMediator mediator,
        IWalletFundingService walletFunding,TimeProvider clock,IOptions<CheckoutOptions> checkoutOptions,
        IOptions<FundingContinuationOptions> options,ILogger<StartCheckoutFundingHandler> logger)
        :this(checkouts,continuations,security,mediator,walletFunding,clock,checkoutOptions,options,
            Microsoft.Extensions.Options.Options.Create(new RiskOptions()),logger) { }

    public async Task<CheckoutFundingContinuationDto> Handle(StartCheckoutFundingCommand request,CancellationToken ct)
    {
        if(!security.TryHashPresentedToken(request.Token,out var tokenHash))throw NotFound();
        var context=await checkouts.GetFundingEvaluationContextAsync(tokenHash,ct)??throw NotFound();
        var now=clock.GetUtcNow();
        FundingContinuationSupport.ValidateCustomerContext(context.Session,context.Payment,
            request.CheckoutAuthorizationSecret,security,now,requireAwaitingFunding:true);
        var customerId=context.Payment.CustomerId!.Value;
        if(context.Payment.OrderId is not { } orderId)
            throw Error("payment_context_invalid","اطلاعات پرداخت معتبر نیست",503);
        var identity=await mediator.Send(new GetUserAuthenticationEligibilityQuery(customerId),ct);
        if(identity.UserId!=customerId||!identity.CanAuthenticate)
            throw Error("checkout_authentication_required","احراز هویت نشست پرداخت معتبر نیست",401);
        var eligibility=await GetEligibility(orderId,customerId,context.Payment,ct);
        if(eligibility.IsSufficient)
        {
            await checkouts.ApplyFundingEvaluationAsync(context.Session.Id,context.Payment.Id,
                customerId,orderId,true,eligibility.TotalEligibleSpendableAmountMinor,0,now,ct);
            return new(false,"not_required",null,"IRR",null,0,false);
        }
        if(eligibility.PermittedPersonalWalletId is not { } walletId)
            throw Error("wallet_topup_unavailable","شارژ کیف‌پول برای این پرداخت در دسترس نیست",409);
        FundingContinuationClaimResult claim;
        var risk=RiskPolicies.CustomerFunding(riskOptions.Value,Guid.Empty,context.Payment.MerchantId,
            context.Payment.TerminalId,customerId,now);
        try
        {
            claim=await continuations.CreateOrGetFundingContinuationAsync(context.Session.Id,
                context.Payment.Id,customerId,orderId,walletId,eligibility.DeficitAmountMinor,
                Math.Max(1,options.Value.MaximumAttemptsPerPayment),risk,now,ct);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception ex) when(risk is not null)
        {
            logger.LogError(ex,"Funding risk decision unavailable. GatewayPaymentId={GatewayPaymentId}",context.Payment.Id);
            throw Error("risk_temporarily_unavailable","بررسی محدودیت شارژ موقتاً ممکن نیست",503);
        }
        if(claim.Outcome==FundingContinuationClaimOutcome.AttemptLimitReached)
            throw Error("funding_attempt_limit_reached","حداکثر دفعات شارژ کیف‌پول انجام شده است",409);
        if(claim.Outcome==FundingContinuationClaimOutcome.RiskDenied)
            throw Error("funding_limit_exceeded","محدودیت روزانه شروع شارژ کیف‌پول عبور کرده است",429);
        var continuation=claim.Continuation!;
        if(continuation.CustomerId!=customerId||continuation.GatewayPaymentId!=context.Payment.Id
            ||continuation.CheckoutSessionId!=context.Session.Id)
            throw Error("payment_context_invalid","اطلاعات پرداخت معتبر نیست",503);
        var returnUrl=BuildReturnUrl(context.Session);
        try
        {
            var created=await walletFunding.CreateBoundAsync(new CreateBoundWalletFundingRequest(
                continuation.CustomerId,continuation.PersonalWalletId,
                continuation.RequiredDeficitMinor,continuation.Currency,returnUrl,
                continuation.ClientReference),ct);
            await continuations.BindWalletFundingSessionAsync(continuation.Id,created.FundingSessionId,now,ct);
            return new(true,"redirect_ready",continuation.RequiredDeficitMinor,
                continuation.Currency,created.RedirectUrl,continuation.RequiredDeficitMinor,false);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception exception)
        {
            logger.LogWarning(exception,
                "WalletFunding creation outcome unknown. GatewayPaymentId={GatewayPaymentId} FundingContinuationId={FundingContinuationId}",
                context.Payment.Id,continuation.Id);
            await continuations.MarkFundingContinuationPendingAsync(continuation.Id,now,
                FundingContinuationSupport.NextRetry(now,continuation.RetryCount,options.Value),ct);
            return new(true,"pending_recovery",continuation.RequiredDeficitMinor,
                continuation.Currency,null,continuation.RequiredDeficitMinor,false);
        }
    }

    private async Task<OrderFundingEligibilityDto> GetEligibility(Guid orderId,Guid customerId,GatewayPayment payment,CancellationToken ct)
    {
        var result=await mediator.Send(new GetOrderFundingEligibilityQuery(orderId,customerId),ct)
            ??throw Error("funding_eligibility_unavailable","بررسی موجودی قابل استفاده موقتاً ممکن نیست",503);
        if(!FundingContinuationSupport.IsEligibilityConsistent(result,payment,orderId,customerId))
            throw Error("payment_context_invalid","اطلاعات پرداخت معتبر نیست",503);
        return result;
    }

    private string BuildReturnUrl(CheckoutSession session)
    {
        var baseUrl=checkoutOptions.Value.PublicBaseUrl.Trim().TrimEnd('/');
        if(!Uri.TryCreate(baseUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https")
            throw new InvalidOperationException("CreditPayments Checkout PublicBaseUrl is invalid.");
        return $"{baseUrl}/c/{security.RecreateCheckoutToken(session)}?funding=return";
    }
    private static CreditPaymentsApplicationException NotFound()=>Error("checkout_not_found","نشست پرداخت یافت نشد",404);
    private static CreditPaymentsApplicationException Error(string code,string message,int status)=>new(code,message,status);
}

public sealed class ReconcileCheckoutFundingHandler(
    IFundingContinuationRepository continuations,
    ICheckoutSecurityService security,
    TimeProvider clock,
    FundingContinuationReconciler reconciler)
    : IRequestHandler<ReconcileCheckoutFundingCommand,CheckoutFundingContinuationDto>
{
    public async Task<CheckoutFundingContinuationDto> Handle(ReconcileCheckoutFundingCommand request,CancellationToken ct)
    {
        if(!security.TryHashPresentedToken(request.Token,out var tokenHash))throw NotFound();
        var context=await continuations.GetFundingContinuationContextAsync(tokenHash,ct)??throw NotFound();
        FundingContinuationSupport.ValidateCustomerContext(context.Session,context.Payment,
            request.CheckoutAuthorizationSecret,security,clock.GetUtcNow(),requireAwaitingFunding:false,
            allowExpired:true);
        return await reconciler.ReconcileAsync(context,ct);
    }
    private static CreditPaymentsApplicationException NotFound()=>new("funding_not_found","ادامه شارژ کیف‌پول یافت نشد",404);
}

public sealed class RecoverFundingContinuationHandler(
    IFundingContinuationRepository continuations,
    FundingContinuationReconciler reconciler)
    : IRequestHandler<RecoverFundingContinuationCommand,Unit>
{
    public async Task<Unit> Handle(RecoverFundingContinuationCommand request,CancellationToken ct)
    {
        var context=await continuations.GetFundingContinuationContextAsync(request.FundingContinuationId,ct);
        if(context is not null)await reconciler.ReconcileAsync(context,ct);
        return Unit.Value;
    }
}

public sealed class FundingContinuationReconciler(
    ICheckoutRepository checkouts,
    IFundingContinuationRepository continuations,
    IMediator mediator,
    IWalletFundingService walletFunding,
    TimeProvider clock,
    IOptions<FundingContinuationOptions> options,
    ILogger<FundingContinuationReconciler> logger)
{
    public async Task<CheckoutFundingContinuationDto> ReconcileAsync(FundingContinuationContext context,CancellationToken ct)
    {
        var c=context.Continuation;
        var now=clock.GetUtcNow();
        if(c.Status==FundingContinuationStatus.Succeeded)
            return await ReevaluateSucceeded(context,now,ct);
        WalletFundingStatus? status;
        try
        {
            status=c.WalletFundingSessionId is { } sessionId
                ?await walletFunding.GetStatusAsync(c.CustomerId,sessionId,ct)
                :await walletFunding.GetStatusByClientReferenceAsync(c.CustomerId,c.ClientReference,ct);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception exception)
        {
            logger.LogWarning(exception,"WalletFunding inquiry failed. FundingContinuationId={FundingContinuationId}",c.Id);
            await Pending(c,now,ct);
            return Dto(c,"pending_recovery",c.RequiredDeficitMinor,false);
        }
        if(status is null)
        {
            await Pending(c,now,ct);
            return Dto(c,"pending_recovery",c.RequiredDeficitMinor,false);
        }
        if(!FundingContinuationSupport.IsFundingStatusConsistent(c,status))
        {
            logger.LogCritical("WalletFunding integrity mismatch. GatewayPaymentId={GatewayPaymentId} FundingContinuationId={FundingContinuationId}",c.GatewayPaymentId,c.Id);
            await checkouts.RecordFundingIntegrityFailureAsync(c.GatewayPaymentId,c.CustomerId,now,ct);
            await Pending(c,now,ct);
            throw new CreditPaymentsApplicationException("funding_integrity_mismatch","نتیجه شارژ کیف‌پول قابل تأیید نیست",503);
        }
        if(c.WalletFundingSessionId is null)
            await continuations.BindWalletFundingSessionAsync(c.Id,status.FundingSessionId,now,ct);
        var normalized=status.Status.ToLowerInvariant();
        if(normalized=="succeeded")
            return await CompleteAndReevaluate(context,FundingContinuationStatus.Succeeded,now,ct);
        if(normalized is "failed")
            return await CompleteAndReevaluate(context,FundingContinuationStatus.Failed,now,ct);
        if(normalized is "expired" or "canceled" or "cancelled")
            return await CompleteAndReevaluate(context,FundingContinuationStatus.Canceled,now,ct);
        await Pending(c,now,ct);
        return Dto(c,"pending_recovery",c.RequiredDeficitMinor,false);
    }

    private async Task<CheckoutFundingContinuationDto> CompleteAndReevaluate(
        FundingContinuationContext context,FundingContinuationStatus terminal,DateTimeOffset now,CancellationToken ct)
    {
        var c=context.Continuation;
        await continuations.CompleteFundingContinuationAsync(c.Id,terminal,now,ct);
        if(now>=context.Payment.ExpiresAt||now>=context.Session.ExpiresAt
            ||context.Payment.Status is GatewayPaymentStatus.Canceled or GatewayPaymentStatus.Expired
                or GatewayPaymentStatus.Succeeded or GatewayPaymentStatus.PartiallyRefunded
                or GatewayPaymentStatus.Refunded)
            return Dto(c,Name(terminal),null,false);
        if(context.Payment.OrderId is not { } orderId)
            throw new CreditPaymentsApplicationException("payment_context_invalid","اطلاعات پرداخت معتبر نیست",503);
        OrderFundingEligibilityDto eligibility;
        try
        {
            eligibility=await mediator.Send(new GetOrderFundingEligibilityQuery(orderId,c.CustomerId),ct)
                ??throw new InvalidOperationException("Order funding eligibility was unavailable.");
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch when(terminal==FundingContinuationStatus.Succeeded)
        {
            await continuations.SchedulePostFundingEvaluationAsync(c.Id,now,
                FundingContinuationSupport.NextRetry(now,c.RetryCount,options.Value),ct);
            throw new CreditPaymentsApplicationException("funding_eligibility_unavailable","بررسی موجودی قابل استفاده موقتاً ممکن نیست",503);
        }
        if(!FundingContinuationSupport.IsEligibilityConsistent(eligibility,context.Payment,orderId,c.CustomerId))
            throw new CreditPaymentsApplicationException("payment_context_invalid","اطلاعات پرداخت معتبر نیست",503);
        if(eligibility.PermittedPersonalWalletId is { } currentWallet&&currentWallet!=c.PersonalWalletId)
            throw new CreditPaymentsApplicationException("funding_integrity_mismatch","نتیجه شارژ کیف‌پول قابل تأیید نیست",503);
        await checkouts.ApplyFundingEvaluationAsync(context.Session.Id,context.Payment.Id,c.CustomerId,
            orderId,eligibility.IsSufficient,eligibility.TotalEligibleSpendableAmountMinor,
            eligibility.DeficitAmountMinor,now,ct);
        if(terminal==FundingContinuationStatus.Succeeded)
            await continuations.CompletePostFundingEvaluationAsync(c.Id,now,ct);
        var ready=terminal==FundingContinuationStatus.Succeeded&&eligibility.IsSufficient;
        if(ready)
        {
            try
            {
                await mediator.Send(new ExecuteGatewayPaymentCommand(
                    context.Payment.Id,"FundingAutoResume"),ct);
                logger.LogInformation(
                    "Automatic payment resume invoked. GatewayPaymentId={GatewayPaymentId} FundingContinuationId={FundingContinuationId}",
                    context.Payment.Id,c.Id);
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(Exception exception)
            {
                // Readiness is durable. AutomaticPaymentResumeWorker will retry after
                // a process failure or any transient immediate-trigger failure.
                logger.LogWarning(exception,
                    "Immediate automatic payment resume failed; durable worker fallback remains available. GatewayPaymentId={GatewayPaymentId} FundingContinuationId={FundingContinuationId}",
                    context.Payment.Id,c.Id);
            }
        }
        return new(!eligibility.IsSufficient,Name(terminal),null,"IRR",null,
            eligibility.DeficitAmountMinor,ready);
    }
    private async Task<CheckoutFundingContinuationDto> ReevaluateSucceeded(FundingContinuationContext context,DateTimeOffset now,CancellationToken ct)
    {
        if(now>=context.Payment.ExpiresAt||now>=context.Session.ExpiresAt
            ||context.Payment.Status is GatewayPaymentStatus.Canceled or GatewayPaymentStatus.Expired
                or GatewayPaymentStatus.Succeeded or GatewayPaymentStatus.PartiallyRefunded
                or GatewayPaymentStatus.Refunded)
        {
            await continuations.CompletePostFundingEvaluationAsync(context.Continuation.Id,now,ct);
            return Dto(context.Continuation,"succeeded",null,false);
        }
        return await CompleteAndReevaluate(context,FundingContinuationStatus.Succeeded,now,ct);
    }
    private Task Pending(FundingContinuation c,DateTimeOffset now,CancellationToken ct)=>
        continuations.MarkFundingContinuationPendingAsync(c.Id,now,
            FundingContinuationSupport.NextRetry(now,c.RetryCount,options.Value),ct);
    private static CheckoutFundingContinuationDto Dto(FundingContinuation c,string status,long? deficit,bool ready)=>
        new(true,status,c.RequiredDeficitMinor,c.Currency,null,deficit,ready);
    private static string Name(FundingContinuationStatus status)=>status.ToString().ToLowerInvariant();
}

internal static class FundingContinuationSupport
{
    public static void ValidateCustomerContext(CheckoutSession session,GatewayPayment payment,
        string? secret,ICheckoutSecurityService security,DateTimeOffset now,bool requireAwaitingFunding,bool allowExpired=false)
    {
        if(!allowExpired&&(now>=payment.ExpiresAt||now>=session.ExpiresAt))
            throw new CreditPaymentsApplicationException("payment_expired","پرداخت منقضی شده است",410);
        if(session.GatewayPaymentId!=payment.Id||session.CustomerId is null||payment.CustomerId!=session.CustomerId)
            throw new CreditPaymentsApplicationException("payment_context_invalid","اطلاعات پرداخت معتبر نیست",503);
        if(requireAwaitingFunding&&(payment.Status!=GatewayPaymentStatus.AwaitingFunding||session.Status!=CheckoutSessionStatus.AwaitingFunding))
            throw new CreditPaymentsApplicationException("funding_not_required","این پرداخت در انتظار شارژ کیف‌پول نیست",409);
        if(string.IsNullOrWhiteSpace(secret)||session.AuthorizationSecretHash is null
            ||session.AuthorizationExpiresAt<=now||!security.VerifyAuthorizationSecret(secret,session.AuthorizationSecretHash))
            throw new CreditPaymentsApplicationException("checkout_authentication_required","احراز هویت نشست پرداخت الزامی است",401);
    }
    public static bool IsEligibilityConsistent(OrderFundingEligibilityDto e,GatewayPayment p,Guid orderId,Guid customerId)
    {
        var expected=Math.Max(0,p.AmountMinor-e.TotalEligibleSpendableAmountMinor);
        return e.OrderId==orderId&&e.UserId==customerId&&e.OrderAmountMinor==p.AmountMinor
            &&e.Currency=="IRR"&&e.DeficitAmountMinor==expected&&e.IsSufficient==(expected==0)
            &&e.HasPermittedPersonalWallet==(e.PermittedPersonalWalletId is not null);
    }
    public static bool IsFundingStatusConsistent(FundingContinuation c,WalletFundingStatus s)=>
        s.FundingSessionId!=Guid.Empty&&(c.WalletFundingSessionId is null||c.WalletFundingSessionId==s.FundingSessionId)
        &&s.UserId==c.CustomerId&&s.WalletId==c.PersonalWalletId
        &&s.AmountMinor==c.RequiredDeficitMinor&&s.Currency==c.Currency
        &&s.ClientReference==c.ClientReference;
    public static DateTimeOffset NextRetry(DateTimeOffset now,int retryCount,FundingContinuationOptions options)
    {
        var exponent=Math.Min(10,Math.Max(0,retryCount));
        var seconds=Math.Min(Math.Max(1,options.MaximumRetrySeconds),
            Math.Max(1,options.BaseRetrySeconds)*(1<<exponent));
        return now.AddSeconds(seconds+Random.Shared.Next(0,Math.Max(2,seconds/5)));
    }
}
