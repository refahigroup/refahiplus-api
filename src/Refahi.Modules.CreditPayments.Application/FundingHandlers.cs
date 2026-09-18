using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;

namespace Refahi.Modules.CreditPayments.Application;

public sealed class EvaluateCheckoutFundingHandler(
    ICheckoutRepository checkouts,
    ICheckoutSecurityService security,
    IMediator mediator,
    TimeProvider clock,
    ILogger<EvaluateCheckoutFundingHandler> logger)
    : IRequestHandler<EvaluateCheckoutFundingCommand, CheckoutFundingEvaluationDto>
{
    private const string SourceModule = "CreditPayments";

    public async Task<CheckoutFundingEvaluationDto> Handle(
        EvaluateCheckoutFundingCommand request,
        CancellationToken cancellationToken)
    {
        if (!security.TryHashPresentedToken(request.Token, out var tokenHash))
            throw Error("checkout_not_found", "نشست پرداخت یافت نشد", 404);

        var context = await checkouts.GetFundingEvaluationContextAsync(tokenHash, cancellationToken)
            ?? throw Error("checkout_not_found", "نشست پرداخت یافت نشد", 404);
        var now = clock.GetUtcNow();
        ValidateAuthenticatedContext(context, request.CheckoutAuthorizationSecret, now);

        var customerId = context.Payment.CustomerId!.Value;
        if (context.Payment.OrderId is null)
        {
            var identity = await mediator.Send(
                new GetUserAuthenticationEligibilityQuery(customerId),
                cancellationToken);
            if (identity.UserId != customerId || !identity.CanAuthenticate)
                throw Error(
                    "checkout_authentication_required",
                    "احراز هویت نشست پرداخت معتبر نیست",
                    401);
        }

        ExternalPaymentOrderDto order;
        try
        {
            order = await mediator.Send(
                new CreateOrGetExternalPaymentOrderCommand(
                    customerId,
                    SourceModule,
                    context.Payment.Id,
                    context.Merchant.SupplierId,
                    context.Payment.AmountMinor,
                    context.Payment.Currency,
                    context.Payment.ApprovedCategoryCodeSnapshot,
                    $"پرداخت به {context.Payment.MerchantDisplayNameSnapshot}",
                    $"cpg-order-{context.Payment.Id:N}",
                    context.Payment.ExpiresAt),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Orders create-or-get failed. GatewayPaymentId={GatewayPaymentId} CheckoutSessionId={CheckoutSessionId} CustomerId={CustomerId}",
                context.Payment.Id,
                context.Session.Id,
                customerId);
            throw Error(
                "order_creation_unavailable",
                "ایجاد سفارش پرداخت موقتاً ممکن نیست",
                503);
        }

        if (!IsOrderConsistent(context, order))
            await FailIntegrityAsync(context, customerId, "order", cancellationToken);

        try
        {
            await checkouts.BindOrderAsync(
                context.Session.Id,
                context.Payment.Id,
                customerId,
                order.OrderId,
                now,
                recovered: !order.WasCreated,
                cancellationToken);
        }
        catch (CheckoutStateException exception)
        {
            if (exception.Code == "payment_context_invalid")
                await FailIntegrityAsync(context, customerId, "order-binding", cancellationToken);
            throw MapState(exception);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Order binding failed. GatewayPaymentId={GatewayPaymentId} OrderId={OrderId}",
                context.Payment.Id,
                order.OrderId);
            throw Error("temporarily_unavailable", "سرویس پرداخت موقتاً در دسترس نیست", 503);
        }

        OrderFundingEligibilityDto eligibility;
        try
        {
            eligibility = await mediator.Send(
                new GetOrderFundingEligibilityQuery(order.OrderId, customerId),
                cancellationToken)
                ?? throw new InvalidOperationException("The bound Order was not visible to eligibility inquiry.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Orders funding eligibility failed. GatewayPaymentId={GatewayPaymentId} OrderId={OrderId}",
                context.Payment.Id,
                order.OrderId);
            throw Error(
                "funding_eligibility_unavailable",
                "بررسی موجودی قابل استفاده موقتاً ممکن نیست",
                503);
        }

        if (!IsEligibilityConsistent(context, order.OrderId, customerId, eligibility))
            await FailIntegrityAsync(context, customerId, "eligibility", cancellationToken);

        FundingStatePersistenceResult persisted;
        try
        {
            persisted = await checkouts.ApplyFundingEvaluationAsync(
                context.Session.Id,
                context.Payment.Id,
                customerId,
                order.OrderId,
                eligibility.IsSufficient,
                eligibility.TotalEligibleSpendableAmountMinor,
                eligibility.DeficitAmountMinor,
                eligibility.EvaluatedAt,
                cancellationToken);
        }
        catch (CheckoutStateException exception)
        {
            throw MapState(exception);
        }

        logger.LogInformation(
            "Funding eligibility evaluated. GatewayPaymentId={GatewayPaymentId} CheckoutSessionId={CheckoutSessionId} CustomerId={CustomerId} OrderId={OrderId} Result={Result} EligibleAmountMinor={EligibleAmountMinor} DeficitAmountMinor={DeficitAmountMinor}",
            context.Payment.Id,
            context.Session.Id,
            customerId,
            order.OrderId,
            eligibility.IsSufficient ? "sufficient" : "insufficient",
            eligibility.TotalEligibleSpendableAmountMinor,
            eligibility.DeficitAmountMinor);

        return new CheckoutFundingEvaluationDto(
            persisted.Payment.Id,
            CreateGatewayPaymentHandler.PaymentStatusName(persisted.Payment.Status),
            persisted.Payment.AmountMinor,
            persisted.Payment.Currency,
            new CheckoutFundingDto(
                eligibility.IsSufficient,
                eligibility.TotalEligibleSpendableAmountMinor,
                eligibility.DeficitAmountMinor,
                eligibility.HasPermittedPersonalWallet,
                eligibility.EvaluatedAt));
    }

    private void ValidateAuthenticatedContext(
        FundingEvaluationContext context,
        string? presentedSecret,
        DateTimeOffset now)
    {
        if (now >= context.Payment.ExpiresAt || now >= context.Session.ExpiresAt)
            throw Error("payment_expired", "پرداخت منقضی شده است", 410);
        if (context.Payment.Status is not (GatewayPaymentStatus.AwaitingCustomer or GatewayPaymentStatus.AwaitingFunding)
            || context.Session.Status is not (CheckoutSessionStatus.Authenticated or CheckoutSessionStatus.AwaitingFunding))
            throw Error("checkout_not_found", "نشست پرداخت یافت نشد", 404);
        if (context.Payment.CustomerId is null
            || context.Session.CustomerId is null
            || context.Payment.CustomerId != context.Session.CustomerId)
            throw Error("payment_context_invalid", "اطلاعات پرداخت معتبر نیست", 503);
        if (string.IsNullOrWhiteSpace(presentedSecret)
            || context.Session.AuthorizationSecretHash is null
            || context.Session.AuthorizationExpiresAt <= now
            || !security.VerifyAuthorizationSecret(
                presentedSecret,
                context.Session.AuthorizationSecretHash))
            throw Error(
                "checkout_authentication_required",
                "احراز هویت نشست پرداخت الزامی است",
                401);
    }

    private static bool IsOrderConsistent(
        FundingEvaluationContext context,
        ExternalPaymentOrderDto order) =>
        order.UserId == context.Payment.CustomerId
        && order.OrderId != Guid.Empty
        && string.Equals(order.SourceModule, SourceModule, StringComparison.Ordinal)
        && order.SourceReferenceId == context.Payment.Id
        && string.Equals(order.ReferenceType, SourceModule, StringComparison.Ordinal)
        && order.SourceOwnerId == context.Merchant.SupplierId
        && order.AmountMinor == context.Payment.AmountMinor
        && string.Equals(order.Currency, "IRR", StringComparison.Ordinal)
        && string.Equals(
            order.CategoryCode,
            context.Payment.ApprovedCategoryCodeSnapshot,
            StringComparison.Ordinal)
        && (context.Payment.OrderId is null || context.Payment.OrderId == order.OrderId);

    private static bool IsEligibilityConsistent(
        FundingEvaluationContext context,
        Guid orderId,
        Guid customerId,
        OrderFundingEligibilityDto eligibility)
    {
        if (eligibility.OrderId != orderId
            || eligibility.UserId != customerId
            || eligibility.OrderAmountMinor != context.Payment.AmountMinor
            || !string.Equals(eligibility.Currency, "IRR", StringComparison.Ordinal)
            || eligibility.TotalEligibleSpendableAmountMinor < 0
            || eligibility.DeficitAmountMinor < 0
            || eligibility.DeficitAmountMinor > context.Payment.AmountMinor)
            return false;

        var expectedDeficit = Math.Max(
            0,
            context.Payment.AmountMinor - eligibility.TotalEligibleSpendableAmountMinor);
        var expectedSufficient = eligibility.TotalEligibleSpendableAmountMinor
            >= context.Payment.AmountMinor;
        return eligibility.DeficitAmountMinor == expectedDeficit
            && eligibility.IsSufficient == expectedSufficient;
    }

    private async Task FailIntegrityAsync(
        FundingEvaluationContext context,
        Guid customerId,
        string category,
        CancellationToken cancellationToken)
    {
        logger.LogCritical(
            "Funding integrity mismatch. GatewayPaymentId={GatewayPaymentId} CheckoutSessionId={CheckoutSessionId} CustomerId={CustomerId} Category={Category}",
            context.Payment.Id,
            context.Session.Id,
            customerId,
            category);
        await checkouts.RecordFundingIntegrityFailureAsync(
            context.Payment.Id,
            customerId,
            clock.GetUtcNow(),
            cancellationToken);
        throw Error("payment_context_invalid", "اطلاعات پرداخت معتبر نیست", 503);
    }

    private static CreditPaymentsApplicationException MapState(CheckoutStateException exception) =>
        exception.Code switch
        {
            "payment_expired" => Error(exception.Code, exception.Message, 410),
            "checkout_not_found" => Error(exception.Code, exception.Message, 404),
            "payment_context_invalid" => Error(exception.Code, exception.Message, 503),
            _ => Error("temporarily_unavailable", "سرویس پرداخت موقتاً در دسترس نیست", 503)
        };

    private static CreditPaymentsApplicationException Error(
        string code,
        string message,
        int status) => new(code, message, status);
}
