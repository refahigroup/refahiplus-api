using Polly;
using Polly.Extensions.Http;
using Refahi.Shared.Services.Notification;

namespace Refahi.Api.Services.Notification;

public static class NotificationDI
{
    private const string NotificationApiHttpClientName = "NotificationApi";

    public static IServiceCollection RegisterNotificationService(this IServiceCollection services, IConfiguration configuration)
    {
        var useInMemory = configuration.GetValue<bool>("NotificationService:UseInMemory", true);

        if (useInMemory)
        {
            // Use in-memory implementation for development
            services.AddScoped<INotificationService, InMemoryNotificationService>();
        }
        else
        {
            // Use HTTP implementation with external API
            var baseUrl = configuration.GetValue<string>("NotificationService:BaseUrl")
                ?? throw new InvalidOperationException("NotificationService:BaseUrl configuration is required");

            // Configure Named HttpClient with Polly policies (shared by OTP and Message clients)
            services.AddHttpClient(NotificationApiHttpClientName, client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                // Force HTTP/1.1 to avoid h2c negotiation failures with servers that don't support HTTP/2 cleartext
                client.DefaultRequestVersion = System.Net.HttpVersion.Version11;
                client.DefaultVersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionOrLower;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.SocketsHttpHandler
            {
                // Evict idle connections before they become stale (most servers have a 30-60s keep-alive timeout)
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(20),
                PooledConnectionLifetime = TimeSpan.FromSeconds(60),
                ConnectTimeout = TimeSpan.FromSeconds(10),
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            // Register OTP API Client (uses shared HttpClient)
            services.AddScoped<OtpApiClient>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(NotificationApiHttpClientName);
                var logger = sp.GetRequiredService<ILogger<OtpApiClient>>();
                return new OtpApiClient(httpClient, logger);
            });

            // Register Message API Client (uses shared HttpClient)
            services.AddScoped<MessageApiClient>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(NotificationApiHttpClientName);
                var logger = sp.GetRequiredService<ILogger<MessageApiClient>>();
                return new MessageApiClient(httpClient, logger);
            });

            // Register HttpNotificationService
            services.AddScoped<INotificationService, HttpNotificationService>();
        }

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        // Exclude ResponseEnded from retries: it means the server actively closed the connection.
        // Retrying won't help when the server is down or consistently rejecting requests, and
        // only adds 2+4+8=14s of backoff delay. Stale-connection recovery is handled by
        // SocketsHttpHandler.PooledConnectionIdleTimeout instead.
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>(ex => ex.HttpRequestError != System.Net.Http.HttpRequestError.ResponseEnded)
            .OrResult(msg =>
                (int)msg.StatusCode >= 500 ||
                msg.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    Console.WriteLine($"OTP API retry attempt {retryAttempt} after {timespan.TotalSeconds}s delay");
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, duration) =>
                {
                    Console.WriteLine($"OTP API circuit breaker opened for {duration.TotalSeconds}s");
                },
                onReset: () =>
                {
                    Console.WriteLine("OTP API circuit breaker reset");
                });
    }
}
