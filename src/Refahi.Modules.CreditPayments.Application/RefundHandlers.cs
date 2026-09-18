using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Modules.Wallets.Application.Contracts.Usecases;

namespace Refahi.Modules.CreditPayments.Application;

internal static class RefundMapping
{
    public static RefundDto ToDto(Refund x)=>new(x.Id,x.GatewayPaymentId,x.MerchantRefundReference,
        x.Status switch{RefundStatus.PendingRecovery=>"pending_recovery",_=>x.Status.ToString().ToLowerInvariant()},
        x.AmountMinor,x.Currency,x.Reason,x.CreatedAt,x.CompletedAt);
    public static string? Normalize(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    public static byte[] Hash(Guid paymentId,long amount,string? reference,string? reason)
    {
        var fields=new[]{"cpg-create-refund-v1",paymentId.ToString("D"),amount.ToString(CultureInfo.InvariantCulture),reference??"",reason??""};
        var canonical=string.Concat(fields.Select(x=>$"{Encoding.UTF8.GetByteCount(x)}:{x}"));
        return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
    }
}

public sealed class CreateRefundHandler(ICreditPaymentsRepository identities,IRefundRepository refunds,
    IMediator mediator,IOptions<RiskOptions> riskOptions,TimeProvider clock,
    Microsoft.Extensions.Logging.ILogger<CreateRefundHandler> logger):IRequestHandler<CreateRefundCommand,RefundDto>
{
    public CreateRefundHandler(ICreditPaymentsRepository identities,IRefundRepository refunds,
        IMediator mediator,TimeProvider clock):this(identities,refunds,mediator,
            Microsoft.Extensions.Options.Options.Create(new RiskOptions()),clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CreateRefundHandler>.Instance) { }

    public async Task<RefundDto> Handle(CreateRefundCommand request,CancellationToken ct)
    {
        try{IdempotencyRecord.ValidateKey(request.IdempotencyKey);}catch(CreditPaymentsDomainException)
        {throw new CreditPaymentsApplicationException("idempotency_key_invalid","کلید تکرارپذیری نامعتبر است");}
        if(request.AmountMinor<=0)throw new CreditPaymentsApplicationException("refund_amount_invalid","مبلغ بازگشت وجه باید مثبت باشد");
        var reference=RefundMapping.Normalize(request.MerchantRefundReference);
        var reason=RefundMapping.Normalize(request.Reason);
        if(reference?.Length>100)throw new CreditPaymentsApplicationException("merchant_refund_reference_invalid","مرجع بازگشت وجه حداکثر ۱۰۰ نویسه است");
        if(reason?.Length>250)throw new CreditPaymentsApplicationException("refund_reason_invalid","دلیل بازگشت وجه حداکثر ۲۵۰ نویسه است");
        var merchant=await identities.GetMerchantAsync(request.MerchantId,false,ct);
        var terminal=await identities.GetTerminalAsync(request.TerminalId,false,ct);
        if(merchant is null||terminal is null||terminal.MerchantId!=merchant.Id||
            merchant.Status is not (MerchantStatus.Active or MerchantStatus.Suspended)||
            terminal.Status is not (TerminalStatus.Active or TerminalStatus.Suspended))
            throw new CreditPaymentsApplicationException("refund_not_allowed","بازگشت وجه برای پذیرنده یا پایانه غیرفعال مجاز نیست",403);
        RefundClaimResult result;
        var risk=RiskPolicies.TerminalRefund(riskOptions.Value,Guid.Empty,request.MerchantId,
            request.TerminalId,request.AmountMinor,clock.GetUtcNow());
        try
        {
            result=await refunds.ClaimRefundAsync(request.PaymentId,request.MerchantId,request.TerminalId,
                request.CredentialId,request.IdempotencyKey,RefundMapping.Hash(request.PaymentId,request.AmountMinor,reference,reason),
                request.AmountMinor,reference,reason,risk,clock.GetUtcNow(),ct);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception ex) when(risk is not null)
        {
            logger.LogError(ex,"Refund risk decision unavailable. GatewayPaymentId={GatewayPaymentId}",request.PaymentId);
            throw new CreditPaymentsApplicationException("risk_temporarily_unavailable",
                "بررسی محدودیت بازگشت وجه موقتاً ممکن نیست",503);
        }
        switch(result.Outcome)
        {
            case RefundClaimOutcome.NotFound:throw new CreditPaymentsApplicationException("payment_not_found","پرداخت یافت نشد",404);
            case RefundClaimOutcome.InvalidState:throw new CreditPaymentsApplicationException("payment_not_refundable","وضعیت پرداخت برای بازگشت وجه مجاز نیست",409);
            case RefundClaimOutcome.AmountExceeded:throw new CreditPaymentsApplicationException("refund_amount_exceeded","مبلغ بازگشت وجه از مبلغ قابل بازگشت بیشتر است",409);
            case RefundClaimOutcome.IdempotencyConflict:throw new CreditPaymentsApplicationException("idempotency_conflict","کلید تکرارپذیری برای درخواست دیگری استفاده شده است",409);
            case RefundClaimOutcome.MerchantReferenceConflict:throw new CreditPaymentsApplicationException("merchant_refund_reference_conflict","مرجع بازگشت وجه قبلاً استفاده شده است",409);
            case RefundClaimOutcome.IntegrityFailure:throw new CreditPaymentsApplicationException("payment_integrity_error","مرجع مالی پرداخت کامل نیست",409);
            case RefundClaimOutcome.RiskDenied:throw new CreditPaymentsApplicationException("refund_limit_exceeded","محدودیت روزانه بازگشت وجه پایانه عبور کرده است",429);
        }
        var refund=result.Refund!;
        if(result.Outcome==RefundClaimOutcome.Existing&&refund.Status is RefundStatus.Succeeded or RefundStatus.Failed)
            return RefundMapping.ToDto(refund);
        return await mediator.Send(new ProcessRefundCommand(refund.Id),ct)??RefundMapping.ToDto(refund);
    }
}

public sealed class ProcessRefundHandler(IRefundRepository refunds,IWalletPaymentExecutor wallet,IMediator mediator,
    IOptions<RefundOptions> options,TimeProvider clock):IRequestHandler<ProcessRefundCommand,RefundDto?>
{
    public async Task<RefundDto?> Handle(ProcessRefundCommand request,CancellationToken ct)
    {
        var context=await refunds.GetRefundContextAsync(request.RefundId,ct);if(context is null)return null;
        if(context.Refund.Status==RefundStatus.Failed)return RefundMapping.ToDto(context.Refund);
        if(context.Refund.Status==RefundStatus.Succeeded)
        {await ProjectOrder(context,ct);return RefundMapping.ToDto((await refunds.GetRefundContextAsync(request.RefundId,ct))!.Refund);}
        if(context.Payment.WalletPaymentId is null)throw new InvalidOperationException("Captured GatewayPayment has no WalletPaymentId.");
        try
        {
            await refunds.RecordRefundAuditAsync(context.Refund.Id,"wallet-refund.requested",clock.GetUtcNow(),ct);
            var response=await wallet.RefundAsync(new RefundPaymentCommand(context.Payment.WalletPaymentId.Value,
                context.Refund.WalletIdempotencyKey,context.Refund.Reason,null,context.Refund.AmountMinor),ct);
            if(response.Status==CommandStatus.InProgress||response.Data is null)
            {await Pending(context.Refund,ct);return RefundMapping.ToDto((await refunds.GetRefundContextAsync(request.RefundId,ct))!.Refund);}
            var data=response.Data;
            if(data.PaymentId!=context.Payment.WalletPaymentId||data.AmountMinor!=context.Refund.AmountMinor||data.Currency!="IRR")
            {await refunds.RecordRefundAuditAsync(context.Refund.Id,"refund.integrity-mismatch",clock.GetUtcNow(),ct);throw new InvalidOperationException("Wallet refund response does not match the immutable CPG claim.");}
            context=await refunds.FinalizeRefundSuccessAsync(context.Refund.Id,data.RefundId,data.CompletedAt,clock.GetUtcNow(),ct);
            await ProjectOrder(context,ct);return RefundMapping.ToDto((await refunds.GetRefundContextAsync(request.RefundId,ct))!.Refund);
        }
        catch(PaymentNotRefundableException)
        {await refunds.MarkRefundFailedAsync(context.Refund.Id,"wallet_not_refundable",clock.GetUtcNow(),ct);return RefundMapping.ToDto((await refunds.GetRefundContextAsync(request.RefundId,ct))!.Refund);}
        catch(PaymentNotFoundException)
        {await refunds.MarkRefundFailedAsync(context.Refund.Id,"wallet_payment_not_found",clock.GetUtcNow(),ct);return RefundMapping.ToDto((await refunds.GetRefundContextAsync(request.RefundId,ct))!.Refund);}
        catch(OperationCanceledException)
        {await Pending(context.Refund,CancellationToken.None);throw;}
        catch(Exception) when(!ct.IsCancellationRequested)
        {await Pending(context.Refund,CancellationToken.None);return RefundMapping.ToDto((await refunds.GetRefundContextAsync(request.RefundId,CancellationToken.None))!.Refund);}
    }
    private async Task Pending(Refund refund,CancellationToken ct)
    {var now=clock.GetUtcNow();await refunds.MarkRefundPendingRecoveryAsync(refund.Id,now,now.Add(Backoff(refund.RetryCount)),ct);}
    private TimeSpan Backoff(int retry)
    {var max=Math.Max(10,options.Value.MaximumBackoffSeconds);var seconds=Math.Min(max,Math.Pow(2,Math.Min(10,retry+1)));var jitter=(retry*17+7)%11;return TimeSpan.FromSeconds(seconds+jitter);}
    private async Task ProjectOrder(RefundContext context,CancellationToken ct)
    {
        if(context.Refund.OrderProjectionStatus!=OrderRefundProjectionStatus.Pending)return;
        try
        {
            if(context.Payment.OrderId is null||context.Payment.WalletPaymentId is null)throw new InvalidOperationException("Order projection references are missing.");
            await mediator.Send(new MarkOrderFullyRefundedCommand(context.Payment.OrderId.Value,
                context.Payment.WalletPaymentId.Value,$"cpg-order-refunded-{context.Payment.Id:N}"),ct);
            await refunds.CompleteOrderRefundProjectionAsync(context.Refund.Id,clock.GetUtcNow(),ct);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception)
        {var now=clock.GetUtcNow();await refunds.ScheduleOrderRefundProjectionAsync(context.Refund.Id,now,now.Add(Backoff(context.Refund.OrderProjectionRetryCount)),CancellationToken.None);}
    }
}

public sealed class GetRefundHandler(IRefundRepository refunds):IRequestHandler<GetRefundQuery,RefundDto?>
{public async Task<RefundDto?> Handle(GetRefundQuery r,CancellationToken ct){var x=await refunds.GetRefundAsync(r.RefundId,r.MerchantId,r.TerminalId,ct);return x is null?null:RefundMapping.ToDto(x);}}
public sealed class ListRefundsHandler(IRefundRepository refunds,IGatewayPaymentRepository payments):IRequestHandler<ListRefundsQuery,IReadOnlyList<RefundDto>?>
{public async Task<IReadOnlyList<RefundDto>?> Handle(ListRefundsQuery r,CancellationToken ct){if(await payments.GetGatewayPaymentAsync(r.PaymentId,r.MerchantId,r.TerminalId,ct) is null)return null;return (await refunds.ListRefundsAsync(r.PaymentId,r.MerchantId,r.TerminalId,ct)).Select(RefundMapping.ToDto).ToArray();}}
