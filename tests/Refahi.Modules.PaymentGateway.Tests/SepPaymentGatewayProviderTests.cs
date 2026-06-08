using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep.Api;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep.Config;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep.Contract;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Refahi.Modules.PaymentGateway.Tests;

public class SepPaymentGatewayProviderTests
{
    [Fact]
    public void BuildRedirectUrl_UsesSep36SendTokenEndpoint()
    {
        var provider = CreateProvider(_ => "{}");

        var url = provider.BuildRedirectUrl("abc 123");

        Assert.Equal("https://sep.shaparak.ir/OnlinePG/SendToken?token=abc%20123", url);
    }

    [Fact]
    public void TokenResponse_DeserializesErrorCode()
    {
        var json = """
        {
            "status": -1,
            "errorCode": "5",
            "errorDesc": "Invalid parameters"
        }
        """;

        var response = JsonSerializer.Deserialize<SepTokenResponse>(json);

        Assert.NotNull(response);
        Assert.Equal(-1, response!.Status);
        Assert.Equal("5", response.ErrorCode);
        Assert.Equal("Invalid parameters", response.ErrorDesc);
    }

    [Fact]
    public void VerifyResponse_DeserializesSep36TransactionDetail()
    {
        var response = JsonSerializer.Deserialize<SepVerifyResponse>(SuccessfulVerifyJson(1000));

        Assert.NotNull(response);
        Assert.True(response!.Success);
        Assert.Equal(0, response.ResultCode);
        Assert.Equal(1000, response.TransactionDetail!.OrginalAmount);
        Assert.Equal(1000, response.TransactionDetail.AffectiveAmount);
        Assert.Equal("621986****8080", response.TransactionDetail.MaskedPan);
        Assert.Equal("100428", response.TransactionDetail.StraceNo);
    }

    [Fact]
    public async Task VerifyAsync_SucceedsOnlyWhenSepSuccessAndAmountMatches()
    {
        var provider = CreateProvider(request =>
            request.RequestUri!.AbsoluteUri.Contains("VerifyTransaction")
                ? SuccessfulVerifyJson(1000)
                : "{}");

        var result = await provider.VerifyAsync(new VerifyRequest("ref-1", 1000));

        Assert.True(result.IsSuccess);
        Assert.Equal(1000, result.VerifiedAmountMinor);
        Assert.Equal(0, result.ResultCode);
    }

    [Fact]
    public async Task VerifyAsync_FailsWhenVerifiedAmountDoesNotMatchExpectedAmount()
    {
        var provider = CreateProvider(request =>
            request.RequestUri!.AbsoluteUri.Contains("VerifyTransaction")
                ? SuccessfulVerifyJson(900)
                : "{}");

        var result = await provider.VerifyAsync(new VerifyRequest("ref-1", 1000));

        Assert.False(result.IsSuccess);
        Assert.Equal(900, result.VerifiedAmountMinor);
        Assert.Equal(0, result.ResultCode);
        Assert.Contains("amount mismatch", result.ErrorMessage);
    }

    [Theory]
    [InlineData(-2, "not found")]
    [InlineData(-6, "verify window")]
    [InlineData(-105, "not found")]
    [InlineData(-104, "inactive")]
    [InlineData(-106, "IP address")]
    [InlineData(5, "reversed")]
    public async Task VerifyAsync_MapsSep36ErrorCodes(int resultCode, string expectedMessagePart)
    {
        var provider = CreateProvider(request =>
            request.RequestUri!.AbsoluteUri.Contains("VerifyTransaction")
                ? $$"""
                  {
                      "TransactionDetail": null,
                      "ResultCode": {{resultCode}},
                      "Success": false
                  }
                  """
                : "{}");

        var result = await provider.VerifyAsync(new VerifyRequest("ref-1", 1000));

        Assert.False(result.IsSuccess);
        Assert.Equal(resultCode, result.ResultCode);
        Assert.Contains(expectedMessagePart, result.ErrorMessage);
    }

    private static SepPaymentGatewayProvider CreateProvider(Func<HttpRequestMessage, string> responseFactory)
    {
        var options = Options.Create(new SepOptions
        {
            TerminalId = "2015",
            TokenUrl = "https://sep.shaparak.ir/onlinepg/onlinepg",
            PaymentBaseUrl = "https://sep.shaparak.ir/OnlinePG/SendToken",
            VerifyUrl = "https://sep.shaparak.ir/verifyTxnRandomSessionkey/ipg/VerifyTransaction",
            ReverseUrl = "https://sep.shaparak.ir/verifyTxnRandomSessionkey/ipg/ReverseTransaction"
        });

        var client = new HttpClient(new StubHttpMessageHandler(responseFactory));
        var apiClient = new SepApiClient(client, options, new TestLogger<SepApiClient>());
        return new SepPaymentGatewayProvider(apiClient, options);
    }

    private static string SuccessfulVerifyJson(long amount) =>
        $$"""
        {
            "TransactionDetail": {
                "RRN": "14226761817",
                "RefNum": "ref-1",
                "MaskedPan": "621986****8080",
                "HashedPan": "hash",
                "TerminalNumber": 2015,
                "OrginalAmount": {{amount}},
                "AffectiveAmount": {{amount}},
                "StraceDate": "2019-09-16 18:11:06",
                "StraceNo": "100428"
            },
            "ResultCode": 0,
            "ResultDescription": "OK",
            "Success": true
        }
        """;

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, string> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseFactory(request), Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopDisposable.Instance;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        public void Dispose()
        {
        }
    }
}
