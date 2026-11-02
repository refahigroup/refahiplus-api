using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Store.Api.Security;
using Refahi.Modules.Store.Domain.Exceptions;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class InPersonTypedErrorFilterTests
{
    [Theory]
    [InlineData("AGREEMENT_TERM_NOT_EFFECTIVE", 409)]
    [InlineData("IDEMPOTENCY_PAYLOAD_MISMATCH", 409)]
    [InlineData("OTP_DELIVERY_REQUIRES_RESEND", 409)]
    [InlineData("PRODUCT_NOT_FOUND", 404)]
    [InlineData("INVALID_MANUAL_AMOUNT", 400)]
    public async Task DomainRejection_LogsCodeAndCorrelationWithoutSensitiveDetails(
        string code,
        int status
    )
    {
        var logger = new RecordingLogger();
        var filter = new InPersonTypedErrorFilter(logger);
        var http = new DefaultHttpContext { TraceIdentifier = "request-trace" };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, null, "Store.Vendor.InPersonOrder.Start"));
        http.Request.QueryString = new QueryString("?mobile=09123456789&otp=123456");
        var context = EndpointFilterInvocationContext.Create(http);
        var exception = new StoreDomainException("پیام خطا با اطلاعات حساس", code);

        var result = await filter.InvokeAsync(context, _ => throw exception);

        var json = Assert.IsType<JsonHttpResult<InPersonErrorResponse>>(result);
        Assert.Equal(status, json.StatusCode);
        Assert.Equal(new InPersonErrorResponse(false, code, exception.Message, status), json.Value);
        Assert.Equal(LogLevel.Warning, logger.Level);
        Assert.Equal(code, logger.Fields["ErrorCode"]);
        Assert.Equal(status, logger.Fields["StatusCode"]);
        Assert.Equal("Store.Vendor.InPersonOrder.Start", logger.Fields["Endpoint"]);
        Assert.Equal("request-trace", logger.Fields["TraceIdentifier"]);
        Assert.Null(logger.Exception);
        var logged = string.Join(" ", logger.Fields.Values);
        Assert.DoesNotContain("09123456789", logged);
        Assert.DoesNotContain("123456", logged);
        Assert.DoesNotContain(exception.Message, logged);
    }

    private sealed class RecordingLogger : ILogger<InPersonTypedErrorFilter>
    {
        public LogLevel Level { get; private set; }
        public Exception? Exception { get; private set; }
        public Dictionary<string, object?> Fields { get; private set; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Level = logLevel;
            Exception = exception;
            Fields = ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary();
        }
    }
}
