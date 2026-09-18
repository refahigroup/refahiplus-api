using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.PaymentGateway.Application.Contracts.Funding;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase06FundingContinuationTests
{
    private static readonly DateTimeOffset Now=new(2026,9,16,20,0,0,TimeSpan.Zero);

    [Fact]
    public void Continuation_preserves_exact_deficit_binding_and_consent()
    {
        var payment=Guid.NewGuid();var checkout=Guid.NewGuid();var customer=Guid.NewGuid();var wallet=Guid.NewGuid();
        var continuation=FundingContinuation.Create(payment,checkout,customer,wallet,3_000_000,Now);
        Assert.Equal(payment,continuation.GatewayPaymentId);
        Assert.Equal(checkout,continuation.CheckoutSessionId);
        Assert.Equal(customer,continuation.CustomerId);
        Assert.Equal(wallet,continuation.PersonalWalletId);
        Assert.Equal(3_000_000,continuation.RequiredDeficitMinor);
        Assert.Equal("IRR",continuation.Currency);
        Assert.Equal(Now,continuation.AutoResumeConsentAt);
        Assert.StartsWith("cpg-funding-",continuation.ClientReference);
        Assert.True(continuation.IsActive);
    }

    [Fact]
    public void Session_binding_is_immutable_and_unknown_remains_recoverable()
    {
        var continuation=FundingContinuation.Create(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),10,Now);
        var session=Guid.NewGuid();
        continuation.BindSession(session,Now);
        continuation.MarkPendingRecovery(Now.AddSeconds(1),Now.AddMinutes(1));
        Assert.Equal(session,continuation.WalletFundingSessionId);
        Assert.Equal(FundingContinuationStatus.PendingRecovery,continuation.Status);
        Assert.True(continuation.IsActive);
        Assert.Throws<CreditPaymentsDomainException>(()=>continuation.BindSession(Guid.NewGuid(),Now));
    }

    [Fact]
    public void Success_failure_and_cancel_are_terminal_without_payment_execution_types()
    {
        foreach(var expected in new[]{FundingContinuationStatus.Succeeded,FundingContinuationStatus.Failed,FundingContinuationStatus.Canceled})
        {
            var continuation=FundingContinuation.Create(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),10,Now);
            if(expected==FundingContinuationStatus.Succeeded)continuation.MarkSucceeded(Now);
            if(expected==FundingContinuationStatus.Failed)continuation.MarkFailed(Now);
            if(expected==FundingContinuationStatus.Canceled)continuation.MarkCanceled(Now);
            Assert.Equal(expected,continuation.Status);Assert.False(continuation.IsActive);Assert.Equal(Now,continuation.CompletedAt);
            if(expected==FundingContinuationStatus.Succeeded)
            {
                continuation.SchedulePostFundingEvaluation(Now,Now.AddMinutes(1));
                Assert.Equal(Now.AddMinutes(1),continuation.NextRetryAt);
                continuation.CompletePostFundingEvaluation(Now.AddSeconds(1));
                Assert.Null(continuation.NextRetryAt);
            }
        }
        var assembly=typeof(StartCheckoutFundingCommand).Assembly;
        Assert.Null(assembly.GetType("Refahi.Modules.CreditPayments.Application.Contracts.PayOrderCommand"));
        Assert.Null(assembly.GetType("Refahi.Modules.CreditPayments.Application.Contracts.PaymentAttempt"));
    }

    [Fact]
    public void Public_start_contract_has_no_financial_or_provider_input()
    {
        var names=typeof(StartCheckoutFundingCommand).GetProperties().Select(x=>x.Name).ToArray();
        Assert.Equal(["Token","CheckoutAuthorizationSecret"],names);
        Assert.DoesNotContain(names,x=>x.Contains("Wallet",StringComparison.OrdinalIgnoreCase)
            ||x.Contains("Amount",StringComparison.OrdinalIgnoreCase)||x.Contains("Provider",StringComparison.OrdinalIgnoreCase));
        var request=typeof(CreateBoundWalletFundingRequest).GetProperties().Select(x=>x.Name).ToArray();
        Assert.DoesNotContain(request,x=>x.Contains("Provider",StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreditPayments_application_references_only_approved_external_contracts()
    {
        var references=typeof(Refahi.Modules.CreditPayments.Application.DI).Assembly.GetReferencedAssemblies().Select(x=>x.Name).ToArray();
        Assert.Contains("Refahi.Modules.PaymentGateway.Application.Contracts",references);
        Assert.DoesNotContain("Refahi.Modules.PaymentGateway.Domain",references);
        Assert.DoesNotContain("Refahi.Modules.PaymentGateway.Infrastructure",references);
        Assert.Contains("Refahi.Modules.Wallets.Application.Contracts",references);
        Assert.DoesNotContain("Refahi.Modules.Wallets.Domain",references);
        Assert.DoesNotContain("Refahi.Modules.Wallets.Infrastructure",references);
    }
}
