using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Refahi.Shared.Presentation;
namespace Refahi.Modules.Orders.Api.Endpoints;

public sealed class PromotionIpLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(ip =>
        RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 100, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    public ValueTask<RateLimitLease> AcquireAsync(string ip, CancellationToken ct) => _limiter.AcquireAsync(ip, 1, ct);
    public void Dispose() => _limiter.Dispose();
}
public sealed class PromotionIpRateLimitFilter(PromotionIpLimiter limiter) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        using var lease = await limiter.AcquireAsync(context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", context.HttpContext.RequestAborted);
        if (!lease.IsAcquired) return Results.Json(ApiResponseHelper.Error("تعداد تلاش‌ها بیش از حد مجاز است؛ کمی بعد تلاش کنید", statusCode: 429), statusCode: 429);
        return await next(context);
    }
}
