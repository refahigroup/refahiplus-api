using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;

namespace Refahi.Modules.CreditPayments.Application;

public sealed class CancelGatewayPaymentHandler(
    IPaymentTerminationRepository terminations,
    IMediator mediator,
    TimeProvider clock)
    : IRequestHandler<CancelGatewayPaymentCommand,GatewayPaymentInquiryDto>
{
    public async Task<GatewayPaymentInquiryDto> Handle(CancelGatewayPaymentCommand request,CancellationToken ct)
    {
        Validate(request.IdempotencyKey,request.Reason);
        var result=await terminations.RequestMerchantCancellationAsync(request.PaymentId,
            request.MerchantId,request.TerminalId,request.IdempotencyKey,
            Hash(request.PaymentId,request.Reason),request.Reason,clock.GetUtcNow(),ct);
        await ResolveIfNeeded(result,request.PaymentId,mediator,ct);
        var current=await terminations.GetTerminationContextAsync(request.PaymentId,ct);
        return MapResult(result,current?.Payment);
    }

    internal static void Validate(string key,string? reason)
    {
        try{IdempotencyRecord.ValidateKey(key);}
        catch(CreditPaymentsDomainException){throw Error("idempotency_key_invalid","کلید تکرارپذیری نامعتبر است",400);}
        if(reason?.Trim().Length>250)throw Error("invalid_cancel_reason","دلیل لغو نامعتبر است",400);
    }
    public static byte[] Hash(Guid paymentId,string? reason)
    {
        var normalized=reason?.Trim()??string.Empty;
        return SHA256.HashData(Encoding.UTF8.GetBytes(
            $"cpg-cancel-payment-v1:{paymentId:D}:{Encoding.UTF8.GetByteCount(normalized)}:{normalized}"));
    }
    internal static async Task ResolveIfNeeded(PaymentTerminationRequestResult result,Guid paymentId,
        IMediator mediator,CancellationToken ct)
    {
        if(result.Outcome==PaymentTerminationRequestOutcome.Processing)
            await mediator.Send(new ResolvePaymentTerminationCommand(paymentId),ct);
    }
    internal static GatewayPaymentInquiryDto MapResult(PaymentTerminationRequestResult result,GatewayPayment? current)
    {
        if(result.Outcome==PaymentTerminationRequestOutcome.NotFound)
            throw Error("payment_not_found","پرداخت یافت نشد",404);
        if(result.Outcome==PaymentTerminationRequestOutcome.IdempotencyConflict)
            throw Error("idempotency_conflict","کلید تکرارپذیری برای درخواست دیگری استفاده شده است",409);
        if(result.Outcome is PaymentTerminationRequestOutcome.Succeeded or PaymentTerminationRequestOutcome.InvalidState)
            throw Error("invalid_payment_state","پرداخت در وضعیت قابل لغو نیست",409);
        var payment=current??result.Payment??throw new InvalidOperationException("Payment cancellation result is missing.");
        return CreateGatewayPaymentHandler.ToInquiryDto(payment);
    }
    internal static CreditPaymentsApplicationException Error(string code,string message,int status)=>new(code,message,status);
}

public sealed class CancelCheckoutPaymentHandler(
    IPaymentExecutionRepository executions,
    IPaymentTerminationRepository terminations,
    ICheckoutSecurityService security,
    IMediator mediator,
    TimeProvider clock)
    : IRequestHandler<CancelCheckoutPaymentCommand,CheckoutPaymentExecutionDto>
{
    public async Task<CheckoutPaymentExecutionDto> Handle(CancelCheckoutPaymentCommand request,CancellationToken ct)
    {
        CancelGatewayPaymentHandler.Validate(request.IdempotencyKey,request.Reason);
        if(!security.TryHashPresentedToken(request.Token,out var tokenHash))throw NotFound();
        var context=await executions.GetPaymentExecutionContextAsync(tokenHash,ct)??throw NotFound();
        var now=clock.GetUtcNow();
        if(context.Session.CustomerId is not { } customerId||context.Payment.CustomerId!=customerId
            ||string.IsNullOrWhiteSpace(request.CheckoutAuthorizationSecret)
            ||context.Session.AuthorizationSecretHash is null
            ||context.Session.AuthorizationExpiresAt<=now
            ||!security.VerifyAuthorizationSecret(request.CheckoutAuthorizationSecret,
                context.Session.AuthorizationSecretHash))
            throw new CreditPaymentsApplicationException("checkout_authentication_required",
                "احراز هویت نشست پرداخت الزامی است",401);
        var result=await terminations.RequestCustomerCancellationAsync(context.Session.Id,
            context.Payment.Id,customerId,request.IdempotencyKey,
            CancelGatewayPaymentHandler.Hash(context.Payment.Id,request.Reason),request.Reason,now,ct);
        await CancelGatewayPaymentHandler.ResolveIfNeeded(result,context.Payment.Id,mediator,ct);
        var current=await terminations.GetTerminationContextAsync(context.Payment.Id,ct);
        _=CancelGatewayPaymentHandler.MapResult(result,current?.Payment);
        return PaymentExecutionSupport.Dto(current?.Payment??result.Payment!);
    }
    private static CreditPaymentsApplicationException NotFound()=>new(
        "checkout_not_found","نشست پرداخت یافت نشد",404);
}

public sealed class ResolvePaymentTerminationHandler(
    IPaymentTerminationRepository terminations,
    IMediator mediator,
    TimeProvider clock,
    IOptions<PaymentExecutionOptions> options,
    ILogger<ResolvePaymentTerminationHandler> logger)
    : IRequestHandler<ResolvePaymentTerminationCommand,Unit>
{
    public async Task<Unit> Handle(ResolvePaymentTerminationCommand request,CancellationToken ct)
    {
        var context=await terminations.GetTerminationContextAsync(request.GatewayPaymentId,ct);
        if(context?.Termination is not { IsUnresolved:true } termination
            ||context.Payment.Status!=GatewayPaymentStatus.Processing
            ||context.ActiveAttempt is not { } attempt
            ||context.Payment.CustomerId is not { } customerId
            ||context.Payment.OrderId is not { } orderId)return Unit.Value;
        try
        {
            var order=await mediator.Send(new GetOrderPaymentExecutionStatusQuery(orderId,customerId),ct);
            if(!PaymentExecutionSupport.IsOrderConsistent(order,context.Payment))
            {
                await terminations.RecordTerminationAuditAsync(context.Payment.Id,customerId,
                    "gateway-payment.termination-integrity-mismatch",clock.GetUtcNow(),ct);
                throw new InvalidOperationException("Orders termination context mismatch.");
            }
            if(order!.ExecutionStatus=="Succeeded"&&order.PaymentId is { } capturedPaymentId)
            {
                await terminations.FinalizeCaptureWonAsync(context.Payment.Id,attempt.Id,
                    order.PaymentIntentId,capturedPaymentId,order.PaidAt??clock.GetUtcNow(),clock.GetUtcNow(),ct);
                return Unit.Value;
            }
            await terminations.RecordTerminationAuditAsync(context.Payment.Id,customerId,
                "gateway-payment.release-requested",clock.GetUtcNow(),ct);
            var result=await mediator.Send(new TerminateOrderPaymentCommand(orderId,customerId,
                termination.ReleaseReference),ct);
            if(result.TerminationStatus=="Captured"&&result.PaymentId is { } paymentId)
            {
                var authoritative=await mediator.Send(new GetOrderPaymentExecutionStatusQuery(orderId,customerId),ct);
                if(authoritative?.ExecutionStatus!="Succeeded"||authoritative.PaymentId!=paymentId)
                {
                    await terminations.RecordTerminationAuditAsync(context.Payment.Id,customerId,
                        "gateway-payment.termination-integrity-mismatch",clock.GetUtcNow(),ct);
                    throw new InvalidOperationException("Captured termination result was not authoritative.");
                }
                await terminations.FinalizeCaptureWonAsync(context.Payment.Id,attempt.Id,
                    authoritative.PaymentIntentId,paymentId,authoritative.PaidAt??clock.GetUtcNow(),
                    clock.GetUtcNow(),ct);
                return Unit.Value;
            }
            if(result.TerminationStatus is "Released" or "AlreadyReleased" or "NoFinancialEffect")
            {
                await terminations.RecordTerminationAuditAsync(context.Payment.Id,customerId,
                    "gateway-payment.release-definitive",clock.GetUtcNow(),ct);
                await terminations.FinalizeTerminationAsync(context.Payment.Id,clock.GetUtcNow(),ct);
                return Unit.Value;
            }
            throw new InvalidOperationException("Order payment termination remains in progress.");
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception ex)
        {
            logger.LogWarning(ex,"Payment termination remains pending. GatewayPaymentId={GatewayPaymentId} PaymentAttemptId={PaymentAttemptId}",
                context.Payment.Id,attempt.Id);
            var now=clock.GetUtcNow();
            await terminations.MarkTerminationPendingRecoveryAsync(context.Payment.Id,attempt.Id,now,
                ExecuteGatewayPaymentHandler.NextRetry(now,Math.Max(attempt.RetryCount,termination.RetryCount),options.Value),ct);
        }
        return Unit.Value;
    }
}
