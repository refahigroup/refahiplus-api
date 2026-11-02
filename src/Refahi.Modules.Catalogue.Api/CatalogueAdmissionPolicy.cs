using System.Diagnostics.Metrics;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Catalogue.Api;

public sealed class CatalogueAdmissionPolicy(IOptions<CatalogueOptions> options) : IRateLimiterPolicy<string>
{
    public const string Name = "CatalogueAdmission";
    private static readonly Meter Meter = new("Refahi.Catalogue");
    private static readonly Counter<long> Rejected = Meter.CreateCounter<long>("catalogue.rejections");

    public RateLimitPartition<string> GetPartition(HttpContext context) =>
        RateLimitPartition.GetConcurrencyLimiter(Name, _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = options.Value.MaxConcurrentRequests,
            QueueLimit = 0,
        });

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => async (context, ct) =>
    {
        Rejected.Add(1, new KeyValuePair<string, object?>("reason", "concurrency"));
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "1";
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        await context.HttpContext.Response.WriteAsJsonAsync(ApiResponseHelper.Error(
            "تعداد درخواست‌ها بیش از حد مجاز است؛ کمی بعد دوباره تلاش کنید.", statusCode: 429), ct);
    };
}
