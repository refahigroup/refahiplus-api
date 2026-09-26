using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure;
using Refahi.Shared.Presentation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace Refahi.Modules.CreditPayments.Api;

public static class DI
{
    public const string ApiPrefix = "/api/credit-payments/v1";
    public const string CheckoutCorsPolicy = "CreditPayments.CheckoutCors";
    public const string PaymentCreatePolicy = "CreditPayments.PaymentCreate";
    public const string PaymentReadPolicy = "CreditPayments.PaymentRead";
    public const string PaymentCancelPolicy = "CreditPayments.PaymentCancel";
    public const string RefundCreatePolicy = "CreditPayments.RefundCreate";
    public const string RefundReadPolicy = "CreditPayments.RefundRead";
    public const string MerchantRateLimitPolicy = "CreditPayments.Merchant";
    public const string CheckoutLookupRateLimitPolicy = "CreditPayments.CheckoutLookup";
    public const string OtpSendRateLimitPolicy = "CreditPayments.OtpSend";
    public const string OtpVerifyRateLimitPolicy = "CreditPayments.OtpVerify";
    public const string FundingEvaluationRateLimitPolicy = "CreditPayments.FundingEvaluation";
    public const string FundingStartRateLimitPolicy = "CreditPayments.FundingStart";
    public const string FundingReconcileRateLimitPolicy = "CreditPayments.FundingReconcile";
    public const string PaymentConfirmRateLimitPolicy = "CreditPayments.PaymentConfirm";

    public static IServiceCollection RegisterCreditPaymentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var checkoutAllowedOrigins = configuration
            .GetSection("CreditPayments:Checkout:AllowedOrigins")
            .Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(
            CheckoutCorsPolicy,
            policy =>
            {
                if (checkoutAllowedOrigins.Length > 0)
                    policy.WithOrigins(checkoutAllowedOrigins);
                policy
                    .WithMethods("GET", "POST", "OPTIONS")
                    .WithHeaders("Accept", "Content-Type", "Idempotency-Key")
                    .AllowCredentials();
            }));

        services
            .RegisterCreditPaymentsApplication(configuration)
            .RegisterCreditPaymentsInfrastructure(configuration);

        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, MerchantBasicAuthenticationHandler>(MerchantBasicAuthenticationHandler.SchemeName,_=>{});
        services.AddAuthorization(options =>
        {
            options.AddPolicy(PaymentCreatePolicy, policy => policy
                .AddAuthenticationSchemes(MerchantBasicAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser().RequireClaim("scope", CredentialScopes.PaymentCreate)
                .RequireClaim("merchant_status","active").RequireClaim("terminal_status","active"));

            options.AddPolicy(PaymentReadPolicy, policy => policy
                .AddAuthenticationSchemes(MerchantBasicAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser().RequireClaim("scope", CredentialScopes.PaymentRead));

            options.AddPolicy(PaymentCancelPolicy, policy => policy
                .AddAuthenticationSchemes(MerchantBasicAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser().RequireClaim("scope", CredentialScopes.PaymentCancel)
                .RequireClaim("merchant_status","active").RequireClaim("terminal_status","active"));

            options.AddPolicy(RefundCreatePolicy, policy => policy
                .AddAuthenticationSchemes(MerchantBasicAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser().RequireClaim("scope",CredentialScopes.RefundCreate)
                .RequireClaim("merchant_status","active","suspended")
                .RequireClaim("terminal_status","active","suspended"));

            options.AddPolicy(RefundReadPolicy, policy => policy
                .AddAuthenticationSchemes(MerchantBasicAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser().RequireClaim("scope",CredentialScopes.RefundRead));
        });
        services.AddRateLimiter(options =>
        {
            AddPolicy(options, MerchantRateLimitPolicy, context =>
                MerchantPartition(context), 120, 60);
            AddPolicy(options, CheckoutLookupRateLimitPolicy, Ip, 60, 60);
            AddPolicy(options, OtpSendRateLimitPolicy, CheckoutPartition, 10, 300);
            AddPolicy(options, OtpVerifyRateLimitPolicy, CheckoutPartition, 20, 300);
            AddPolicy(options, FundingEvaluationRateLimitPolicy, CheckoutPartition, 30, 60);
            AddPolicy(options, FundingStartRateLimitPolicy, CheckoutPartition, 5, 300);
            AddPolicy(options, FundingReconcileRateLimitPolicy, CheckoutPartition, 60, 60);
            AddPolicy(options, PaymentConfirmRateLimitPolicy, CheckoutPartition, 10, 60);
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ApiResponseHelper.Error("تعداد درخواست‌ها بیش از حد مجاز است",
                        new Dictionary<string, string[]> { ["code"] = ["rate_limited"] }, 429),
                    cancellationToken);
            };
        });

        return services;
    }

    public static WebApplication UseCreditPaymentsModule(this WebApplication app, string endPointsPrefix = ApiPrefix)
    {
        var group=app.MapGroup(endPointsPrefix);

        var endpoints = typeof(DI)
            .Assembly
            .GetTypes()
            .Where(
                t => t.IsClass &&
                !t.IsAbstract &&
                typeof(IEndpoint).IsAssignableFrom(t)
            );

        foreach (var type in endpoints)
            if(Activator.CreateInstance(type) is IEndpoint endpoint)
                endpoint.Map(group);

        app.Services.UseInfrastructure(app.Environment.IsDevelopment());

        return app;
    }

    private static void AddPolicy(
        RateLimiterOptions options,
        string name,
        Func<HttpContext, string> partition,
        int permitLimit,
        int windowSeconds)
    {
        options.AddPolicy(name, context =>
            RateLimitPartition.GetFixedWindowLimiter(partition(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }

    private static string CheckoutPartition(HttpContext context)
    {
        var token = context.Request.RouteValues.TryGetValue("token", out var value)
            ? value?.ToString()
            : null;

        var fingerprint = string.IsNullOrEmpty(token)
            ? "invalid"
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        return $"{Ip(context)}:{fingerprint}";
    }

    private static string MerchantPartition(HttpContext context)
    {
        var terminal = context
            .User.FindFirst("terminal_id")?
            .Value;

        if(!string.IsNullOrWhiteSpace(terminal))
            return $"terminal:{terminal}:ip:{Ip(context)}";

        var header=context.Request.Headers.Authorization.ToString();

        string client="invalid";

        try
        {
            if(header.StartsWith("Basic ",StringComparison.OrdinalIgnoreCase))
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
                var separator = decoded.IndexOf(':');

                if (separator > 0)
                    client = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(decoded[..separator])));
            }
        }
        catch(FormatException)
        {
        }

        return $"client:{client}:ip:{Ip(context)}";
    }

    private static string Ip(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
