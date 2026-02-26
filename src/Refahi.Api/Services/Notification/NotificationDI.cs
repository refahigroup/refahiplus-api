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
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .SetHandlerLifetime(TimeSpan.FromMinutes(5)); // HttpClient pooling

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
        return HttpPolicyExtensions
            .HandleTransientHttpError() // 5xx, 408
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) // 429
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    // Log retry attempts
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
