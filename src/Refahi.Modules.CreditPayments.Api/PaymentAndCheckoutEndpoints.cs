using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.CreditPayments.Api;

public sealed class MerchantPaymentEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapPost("/payments", Create)
            .WithName("CreditPayments.CreatePayment")
            .WithTags("CreditPayments.Payments")
            .RequireAuthorization(DI.PaymentCreatePolicy)
            .RequireRateLimiting(DI.MerchantRateLimitPolicy)
            .Produces<ApiResponse<CreateGatewayPaymentResponse>>(StatusCodes.Status201Created);
        routes.MapGet("/payments/{paymentId:guid}", Get)
            .WithName("CreditPayments.GetPayment")
            .WithTags("CreditPayments.Payments")
            .RequireAuthorization(DI.PaymentReadPolicy)
            .RequireRateLimiting(DI.MerchantRateLimitPolicy)
            .Produces<ApiResponse<GatewayPaymentInquiryDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
        routes.MapPost("/payments/{paymentId:guid}/cancel",Cancel)
            .WithName("CreditPayments.CancelPayment")
            .WithTags("CreditPayments.Payments")
            .RequireAuthorization(DI.PaymentCancelPolicy)
            .RequireRateLimiting(DI.MerchantRateLimitPolicy)
            .Produces<ApiResponse<GatewayPaymentInquiryDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<GatewayPaymentInquiryDto>>(StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> Create(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] CreatePaymentRequest body,
        ClaimsPrincipal principal,
        IMediator mediator,
        CancellationToken ct)
    {
        var context = MerchantContext(principal);
        var result = await mediator.Send(new CreateGatewayPaymentCommand(
            context.MerchantId, context.TerminalId, context.CredentialId,
            idempotencyKey ?? string.Empty, body.MerchantOrderId, body.AmountMinor, body.Currency,
            body.Description, body.ReturnUrl, body.ExpiresInSeconds, body.Metadata), ct);
        return Results.Created($"payments/{result.PaymentId:D}",
            ApiResponseHelper.Success(result, "درخواست پرداخت ایجاد شد", StatusCodes.Status201Created));
    }

    private static async Task<IResult> Get(
        Guid paymentId,
        ClaimsPrincipal principal,
        IMediator mediator,
        CancellationToken ct)
    {
        var context = MerchantContext(principal);
        var result = await mediator.Send(new GetGatewayPaymentQuery(
            context.MerchantId, context.TerminalId, paymentId), ct);
        return result is null
            ? Results.NotFound(ApiResponseHelper.Error("پرداخت یافت نشد",
                new Dictionary<string, string[]> { ["code"] = ["payment_not_found"] }, 404))
            : Results.Ok(ApiResponseHelper.Success(result, "اطلاعات پرداخت دریافت شد"));
    }

    private static async Task<IResult> Cancel(
        Guid paymentId,
        [FromHeader(Name="Idempotency-Key")] string? idempotencyKey,
        [FromBody] CancelPaymentRequest? body,
        ClaimsPrincipal principal,
        IMediator mediator,
        CancellationToken ct)
    {
        var context=MerchantContext(principal);
        var result=await mediator.Send(new CancelGatewayPaymentCommand(context.MerchantId,
            context.TerminalId,context.CredentialId,paymentId,idempotencyKey??string.Empty,
            body?.Reason),ct);
        var response=ApiResponseHelper.Success(result,result.Status=="processing"
            ?"خاتمه پرداخت در حال بررسی است":"وضعیت پرداخت به‌روزرسانی شد",
            result.Status=="processing"?StatusCodes.Status202Accepted:StatusCodes.Status200OK);
        return result.Status=="processing"?Results.Accepted(value:response):Results.Ok(response);
    }

    private static (Guid MerchantId, Guid TerminalId, Guid CredentialId) MerchantContext(ClaimsPrincipal principal) =>
        (Guid.Parse(principal.FindFirstValue("merchant_id")!),
         Guid.Parse(principal.FindFirstValue("terminal_id")!),
         Guid.Parse(principal.FindFirstValue("credential_id")!));
}

public sealed record CreatePaymentRequest(
    string MerchantOrderId,
    long AmountMinor,
    string Currency,
    string? Description,
    string ReturnUrl,
    int? ExpiresInSeconds,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed class CheckoutEndpoints : IEndpoint
{
    private const string AuthorizationCookie = "__Host-refahi-cpg-checkout";

    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapGet("/checkout/{token}", Get)
            .WithName("CreditPayments.GetCheckout")
            .WithTags("CreditPayments.Checkout")
            .RequireRateLimiting(DI.CheckoutLookupRateLimitPolicy);
        routes.MapPost("/checkout/{token}/otp/send", SendOtp)
            .WithName("CreditPayments.SendCheckoutOtp")
            .WithTags("CreditPayments.Checkout")
            .RequireRateLimiting(DI.OtpSendRateLimitPolicy);
        routes.MapPost("/checkout/{token}/otp/verify", VerifyOtp)
            .WithName("CreditPayments.VerifyCheckoutOtp")
            .WithTags("CreditPayments.Checkout")
            .RequireRateLimiting(DI.OtpVerifyRateLimitPolicy);
        routes.MapPost("/checkout/{token}/funding/evaluate", EvaluateFunding)
            .WithName("CreditPayments.EvaluateCheckoutFunding")
            .WithTags("CreditPayments.Checkout")
            .RequireRateLimiting(DI.FundingEvaluationRateLimitPolicy)
            .Produces<ApiResponse<CheckoutFundingEvaluationDto>>(StatusCodes.Status200OK);
        routes.MapPost("/checkout/{token}/funding/start", StartFunding)
            .WithName("CreditPayments.StartCheckoutFunding")
            .WithTags("CreditPayments.Checkout")
            .RequireRateLimiting(DI.FundingStartRateLimitPolicy)
            .Produces<ApiResponse<CheckoutFundingContinuationDto>>(StatusCodes.Status200OK);
        routes.MapPost("/checkout/{token}/funding/reconcile", ReconcileFunding)
            .WithName("CreditPayments.ReconcileCheckoutFunding")
            .WithTags("CreditPayments.Checkout")
            .RequireRateLimiting(DI.FundingReconcileRateLimitPolicy)
            .Produces<ApiResponse<CheckoutFundingContinuationDto>>(StatusCodes.Status200OK);
        routes.MapPost("/checkout/{token}/confirm", ConfirmPayment)
            .WithName("CreditPayments.ConfirmCheckoutPayment")
            .WithTags("CreditPayments.Checkout")
            .RequireRateLimiting(DI.PaymentConfirmRateLimitPolicy)
            .Produces<ApiResponse<CheckoutPaymentExecutionDto>>(StatusCodes.Status200OK);
        routes.MapPost("/checkout/{token}/cancel",CancelPayment)
            .WithName("CreditPayments.CancelCheckoutPayment")
            .WithTags("CreditPayments.Checkout")
            .RequireRateLimiting(DI.PaymentConfirmRateLimitPolicy)
            .Produces<ApiResponse<CheckoutPaymentExecutionDto>>(StatusCodes.Status200OK)
            .Produces<ApiResponse<CheckoutPaymentExecutionDto>>(StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> Get(string token, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetCheckoutQuery(token), ct);
        return result is null
            ? NotFound()
            : Results.Ok(ApiResponseHelper.Success(result, "اطلاعات پرداخت دریافت شد"));
    }

    private static async Task<IResult> SendOtp(
        string token,
        [FromBody] SendCheckoutOtpRequest body,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new SendCheckoutOtpCommand(token, body.Identifier), ct);
        return Results.Ok(ApiResponseHelper.Success(result, result.Message));
    }

    private static async Task<IResult> VerifyOtp(
        string token,
        [FromBody] VerifyCheckoutOtpRequest body,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken ct)
    {
        httpContext.Request.Cookies.TryGetValue(AuthorizationCookie, out var existingSecret);
        var result = await mediator.Send(new VerifyCheckoutOtpCommand(
            token, body.ChallengeId, body.Otp, existingSecret), ct);
        if (!string.IsNullOrWhiteSpace(result.AuthorizationSecret) && result.AuthorizationExpiresAt is { } expiresAt)
        {
            httpContext.Response.Cookies.Append(AuthorizationCookie, result.AuthorizationSecret,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                    IsEssential = true,
                    Expires = expiresAt
                });
        }
        return Results.Ok(ApiResponseHelper.Success(
            new { authenticationStatus = result.AuthenticationStatus }, "احراز هویت با موفقیت انجام شد"));
    }

    private static async Task<IResult> EvaluateFunding(
        string token,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken ct)
    {
        httpContext.Request.Cookies.TryGetValue(AuthorizationCookie, out var authorizationSecret);
        var result = await mediator.Send(
            new EvaluateCheckoutFundingCommand(token, authorizationSecret),
            ct);
        return Results.Ok(ApiResponseHelper.Success(result, "وضعیت موجودی قابل استفاده بررسی شد"));
    }

    private static async Task<IResult> StartFunding(string token,HttpContext httpContext,IMediator mediator,CancellationToken ct)
    {
        httpContext.Request.Cookies.TryGetValue(AuthorizationCookie,out var secret);
        var result=await mediator.Send(new StartCheckoutFundingCommand(token,secret),ct);
        return Results.Ok(ApiResponseHelper.Success(result,"درخواست شارژ کیف‌پول ثبت شد"));
    }

    private static async Task<IResult> ReconcileFunding(string token,HttpContext httpContext,IMediator mediator,CancellationToken ct)
    {
        httpContext.Request.Cookies.TryGetValue(AuthorizationCookie,out var secret);
        var result=await mediator.Send(new ReconcileCheckoutFundingCommand(token,secret),ct);
        return Results.Ok(ApiResponseHelper.Success(result,"وضعیت شارژ کیف‌پول بررسی شد"));
    }

    private static async Task<IResult> ConfirmPayment(
        string token,HttpContext httpContext,IMediator mediator,CancellationToken ct)
    {
        httpContext.Request.Cookies.TryGetValue(AuthorizationCookie,out var secret);
        var result=await mediator.Send(new ConfirmCheckoutPaymentCommand(token,secret),ct);
        return Results.Ok(ApiResponseHelper.Success(result,"وضعیت اجرای پرداخت دریافت شد"));
    }

    private static async Task<IResult> CancelPayment(
        string token,[FromHeader(Name="Idempotency-Key")]string? idempotencyKey,
        [FromBody]CancelPaymentRequest? body,HttpContext httpContext,IMediator mediator,CancellationToken ct)
    {
        httpContext.Request.Cookies.TryGetValue(AuthorizationCookie,out var secret);
        var result=await mediator.Send(new CancelCheckoutPaymentCommand(token,secret,
            idempotencyKey??string.Empty,body?.Reason),ct);
        var response=ApiResponseHelper.Success(result,result.Status=="processing"
            ?"خاتمه پرداخت در حال بررسی است":"وضعیت پرداخت به‌روزرسانی شد",
            result.Status=="processing"?StatusCodes.Status202Accepted:StatusCodes.Status200OK);
        return result.Status=="processing"?Results.Accepted(value:response):Results.Ok(response);
    }

    private static IResult NotFound() => Results.NotFound(ApiResponseHelper.Error(
        "نشست پرداخت یافت نشد",
        new Dictionary<string, string[]> { ["code"] = ["checkout_not_found"] }, 404));
}

public sealed record SendCheckoutOtpRequest(string Identifier);
public sealed record VerifyCheckoutOtpRequest(Guid ChallengeId, string Otp);
