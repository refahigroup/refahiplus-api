using System.Reflection;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Cinema.Api.Endpoints;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Shared.Presentation;
using Xunit;

namespace Refahi.Modules.Cinema.Tests;

public sealed class CinemaEndpointTests
{
    [Fact]
    public async Task Unexpected_failure_is_logged_with_response_trace_without_sensitive_messages()
    {
        var logger = new RecordingLogger();
        await using var app = CreateApp(new InvalidOperationException("private payload", new ArgumentException("private customer")), logger);
        var context = await InvokeCities(app);
        var error = JsonSerializer.Deserialize<ApiErrorResponse>(context.Response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(503, context.Response.StatusCode);
        Assert.False(error.Success);
        Assert.Equal("cinema-test-trace", error.TraceId);
        Assert.Equal("سرویس سینما در حال حاضر پاسخگو نیست؛ دوباره تلاش کنید", error.Message);
        var log = Assert.Single(logger.Messages);
        Assert.Contains(error.TraceId, log);
        Assert.Contains("Cinema.Cities", log);
        Assert.Contains(nameof(InvalidOperationException), log);
        Assert.Contains(nameof(ArgumentException), log);
        Assert.DoesNotContain("private", log);
    }

    [Fact]
    public async Task Known_rejection_preserves_status_and_message()
    {
        var logger = new RecordingLogger();
        await using var app = CreateApp(new CinemaException("سانس یافت نشد", 404), logger);
        var context = await InvokeCities(app);
        var error = JsonSerializer.Deserialize<ApiErrorResponse>(context.Response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(404, context.Response.StatusCode);
        Assert.Equal("سانس یافت نشد", error.Message);
        Assert.Empty(logger.Messages);
    }

    [Fact]
    public async Task Cancellation_is_propagated()
    {
        var logger = new RecordingLogger();
        await using var app = CreateApp(new OperationCanceledException(), logger);
        await Assert.ThrowsAsync<OperationCanceledException>(() => InvokeCities(app));
        Assert.Empty(logger.Messages);
    }

    private static WebApplication CreateApp(Exception error, RecordingLogger logger)
    {
        var builder = WebApplication.CreateBuilder();
        var mediator = DispatchProxy.Create<IMediator, ThrowingProxy>();
        ((ThrowingProxy)mediator).Error = error;
        builder.Services.AddSingleton(mediator);
        builder.Services.AddSingleton<ICinemaTicketRenderer>(DispatchProxy.Create<ICinemaTicketRenderer, ThrowingProxy>());
        builder.Services.AddSingleton<ILogger<CinemaEndpoints>>(logger);
        var app = builder.Build();
        new CinemaEndpoints().Map(app.MapGroup("/api/cinema"));
        return app;
    }

    private static async Task<DefaultHttpContext> InvokeCities(WebApplication app)
    {
        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints)
            .Single(x => x.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "Cinema.Cities");
        var context = new DefaultHttpContext { RequestServices = app.Services, TraceIdentifier = "cinema-test-trace" };
        context.Request.Method = "GET";
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        await endpoint.RequestDelegate!(context);
        context.Response.Body.Position = 0;
        return context;
    }

    public class ThrowingProxy : DispatchProxy
    {
        public Exception Error = new NotSupportedException();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw Error;
    }

    private sealed class RecordingLogger : ILogger<CinemaEndpoints>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.Equal(LogLevel.Error, level);
            Assert.Null(exception);
            Messages.Add(formatter(state, exception));
        }
    }
}
