using Microsoft.Extensions.Diagnostics.HealthChecks;
using Refahi.Modules.Charge.Infrastructure.Providers.Eniac;

namespace Refahi.Modules.Charge.Infrastructure.Health;

public sealed class EniacHealthCheck(EniacApiClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var _ = await client.GetAsync("/api/Wallet/GetBallance", cancellationToken, "HealthCheck");
            return HealthCheckResult.Healthy("اتصال و احراز هویت Eniac برقرار است");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("اتصال یا احراز هویت Eniac ناموفق است", ex);
        }
    }
}
