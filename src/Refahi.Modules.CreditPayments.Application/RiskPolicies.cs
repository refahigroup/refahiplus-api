using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Application;

internal static class RiskPolicies
{
    public static RiskReservationRequest? TerminalPayment(RiskOptions options, Guid resourceId,
        Guid merchantId, Guid terminalId, long amountMinor, DateTimeOffset now)
    {
        var rule=options.TerminalOverrides.SingleOrDefault(x=>x.TerminalId==terminalId)?.Payments
                 ??options.TerminalPayments;
        return Build(rule,RiskScopeType.Terminal,terminalId,RiskOperationTypes.PaymentCreate,
            resourceId,amountMinor,RiskRuleCodes.TerminalDailyPaymentCount,
            RiskRuleCodes.TerminalDailyPaymentAmount,merchantId,terminalId,null,now);
    }

    public static RiskReservationRequest? CustomerPayment(RiskOptions options, Guid resourceId,
        Guid merchantId, Guid terminalId, Guid customerId, long amountMinor, DateTimeOffset now) =>
        Build(options.CustomerPayments,RiskScopeType.Customer,customerId,RiskOperationTypes.PaymentExecution,
            resourceId,amountMinor,RiskRuleCodes.CustomerDailyPaymentCount,
            RiskRuleCodes.CustomerDailyPaymentAmount,merchantId,terminalId,customerId,now);

    public static RiskReservationRequest? TerminalRefund(RiskOptions options, Guid resourceId,
        Guid merchantId, Guid terminalId, long amountMinor, DateTimeOffset now)
    {
        var rule=options.TerminalOverrides.SingleOrDefault(x=>x.TerminalId==terminalId)?.Refunds
                 ??options.TerminalRefunds;
        return Build(rule,RiskScopeType.Terminal,terminalId,RiskOperationTypes.RefundCreate,
            resourceId,amountMinor,RiskRuleCodes.TerminalDailyRefundCount,
            RiskRuleCodes.TerminalDailyRefundAmount,merchantId,terminalId,null,now);
    }

    public static RiskReservationRequest? CustomerFunding(RiskOptions options, Guid resourceId,
        Guid merchantId, Guid terminalId, Guid customerId, DateTimeOffset now) =>
        Build(options.CustomerFundingStarts,RiskScopeType.Customer,customerId,RiskOperationTypes.FundingStart,
            resourceId,0,RiskRuleCodes.FundingAttemptLimit,RiskRuleCodes.FundingAttemptLimit,
            merchantId,terminalId,customerId,now);

    private static RiskReservationRequest? Build(RiskLimitOptions rule,RiskScopeType scopeType,Guid scopeId,
        string operationType,Guid resourceId,long amount,string countCode,string amountCode,
        Guid? merchantId,Guid? terminalId,Guid? customerId,DateTimeOffset now)
    {
        if(!rule.Enabled)return null;
        return new(scopeType,scopeId,operationType,resourceId,amount,rule.DailyCountLimit,
            rule.DailyAmountLimitMinor,countCode,amountCode,merchantId,terminalId,customerId,UtcDay(now));
    }

    private static DateTimeOffset UtcDay(DateTimeOffset now)
    {
        var utc=now.ToUniversalTime();
        return new DateTimeOffset(utc.Year,utc.Month,utc.Day,0,0,0,TimeSpan.Zero);
    }
}
