using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Abstraction;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar;

internal sealed class AabsarObservabilityHandler(ILogger<AabsarObservabilityHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await base.SendAsync(request, ct);
            logger.LogInformation("Commerce provider request completed. ProviderKey={ProviderKey}, Method={Method}, Path={Path}, StatusCode={StatusCode}, LatencyMs={LatencyMs}",
                "aabsar", request.Method.Method, request.RequestUri?.AbsolutePath, (int)response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Commerce provider request failed. ProviderKey={ProviderKey}, Method={Method}, Path={Path}, LatencyMs={LatencyMs}",
                "aabsar", request.Method.Method, request.RequestUri?.AbsolutePath, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}

internal sealed class AabsarHealthCheck(IAabsarApiClient api) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try { await api.HealthCheckAsync(ct); return HealthCheckResult.Healthy(); }
        catch (Exception ex) { return HealthCheckResult.Unhealthy("ارتباط با تامین‌کننده آبسار برقرار نیست", ex); }
    }
}
