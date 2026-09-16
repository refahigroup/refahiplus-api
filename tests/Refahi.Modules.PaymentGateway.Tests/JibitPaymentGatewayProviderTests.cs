using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Api;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Config;
using Xunit;

namespace Refahi.Modules.PaymentGateway.Tests;

public sealed class JibitPaymentGatewayProviderTests
{
    [Fact]
    public async Task CreatePurchase_ReturnsDurablePurchaseIdentity()
    {
        var provider = CreateProvider(new JibitHandler());

        var result = await provider.GetTokenAsync(
            new GetTokenRequest("session-1", 1000, "https://api.test/callback"));

        Assert.True(result.IsSuccess);
        Assert.Equal("purchase-1", result.ProviderAttemptId);
        Assert.Equal("purchase-1", result.ProviderTransactionId);
        Assert.Equal("https://switch.test/1", result.Token);
    }

    [Fact]
    public async Task Verify_UsesAuthoritativeInquiryAmountCurrencyAndClientReference()
    {
        var provider = CreateProvider(new JibitHandler(amount: 900));

        var result = await provider.VerifyAsync(new VerifyRequest(
            "purchase-1", "purchase-1", "session-1", 1000, "IRR"));

        Assert.Equal(FundingProviderOutcome.VerifiedSuccess, result.Outcome);
        Assert.Equal(900, result.VerifiedAmountMinor);
        Assert.Equal("IRR", result.VerifiedCurrency);
        Assert.Equal("session-1", result.ClientReference);
        Assert.NotEqual(1000, result.VerifiedAmountMinor);
    }

    [Fact]
    public async Task VerifyUnknown_RemainsUnknownAndDoesNotFabricateAmount()
    {
        var provider = CreateProvider(new JibitHandler(verifyStatus: "UNKNOWN"));

        var result = await provider.VerifyAsync(new VerifyRequest(
            "purchase-1", "purchase-1", "session-1", 1000, "IRR"));

        Assert.Equal(FundingProviderOutcome.Unknown, result.Outcome);
        Assert.Null(result.VerifiedAmountMinor);
        Assert.Null(result.VerifiedCurrency);
    }

    [Fact]
    public async Task Authentication401_RegeneratesTokenOnce()
    {
        var handler = new JibitHandler(unauthorizedPurchaseOnce: true);
        var provider = CreateProvider(handler);

        var result = await provider.GetTokenAsync(
            new GetTokenRequest("session-1", 1000, "https://api.test/callback"));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.TokenCalls);
        Assert.Equal(2, handler.CreateCalls);
    }

    [Fact]
    public async Task CreateProviderError_IsDefinitiveTokenFailure()
    {
        var provider = CreateProvider(new JibitHandler(createError: true));

        var result = await provider.GetTokenAsync(
            new GetTokenRequest("session-1", 1000, "https://api.test/callback"));

        Assert.False(result.IsSuccess);
        Assert.False(result.IsOutcomeUnknown);
        Assert.Contains("rejected", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyServerError_IsUnknownNotDefinitiveFailure()
    {
        var provider = CreateProvider(new JibitHandler(verifyServerError: true));

        var result = await provider.VerifyAsync(new VerifyRequest(
            "purchase-1", "purchase-1", "session-1", 1000, "IRR"));

        Assert.Equal(FundingProviderOutcome.Unknown, result.Outcome);
        Assert.Null(result.VerifiedAmountMinor);
    }

    private static JibitPaymentGatewayProvider CreateProvider(HttpMessageHandler handler)
    {
        var options = Options.Create(new JibitOptions
        {
            ApiKey = "key",
            SecretKey = "secret",
            BaseUrl = "https://jibit.test/ppg/v3",
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl + "/") };
        var api = new JibitApiClient(client, options, new MemoryCache(new MemoryCacheOptions()),
            NullLogger<JibitApiClient>.Instance);
        return new JibitPaymentGatewayProvider(api, options,
            NullLogger<JibitPaymentGatewayProvider>.Instance);
    }

    private sealed class JibitHandler(
        long amount = 1000,
        string verifyStatus = "SUCCESSFUL",
        bool unauthorizedPurchaseOnce = false,
        bool createError = false,
        bool verifyServerError = false) : HttpMessageHandler
    {
        public int TokenCalls { get; private set; }
        public int CreateCalls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            if (path.EndsWith("/tokens", StringComparison.Ordinal))
            {
                TokenCalls++;
                return Json("{\"accessToken\":\"access\",\"refreshToken\":\"refresh\"}");
            }
            if (request.Method == HttpMethod.Post && path.EndsWith("/purchases", StringComparison.Ordinal))
            {
                CreateCalls++;
                if (unauthorizedPurchaseOnce && CreateCalls == 1)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
                if (createError)
                    return Json("{\"errors\":[{\"code\":\"bad.request\",\"message\":\"rejected\"}]}");
                return Json("{\"purchaseId\":\"purchase-1\",\"pspSwitchingUrl\":\"https://switch.test/1\"}");
            }
            if (path.Contains("/purchases/purchase-1/verify", StringComparison.Ordinal))
            {
                if (verifyServerError)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                return Json($"{{\"status\":\"{verifyStatus}\"}}");
            }
            if (path.Contains("/purchases?criteria=", StringComparison.Ordinal))
                return Json($"{{\"elements\":[{{\"purchaseIdStr\":\"purchase-1\",\"amount\":{amount},\"currency\":\"IRR\",\"state\":\"SUCCESS\",\"clientReferenceNumber\":\"session-1\"}}]}}");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static Task<HttpResponseMessage> Json(string json) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }
}
