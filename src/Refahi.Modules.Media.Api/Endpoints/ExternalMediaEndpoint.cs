using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.Media.Infrastructure.Services;
using Refahi.Shared.Presentation;
namespace Refahi.Modules.Media.Api.Endpoints;
public sealed class ExternalMediaEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapMethods("/media/external/{id:guid}", ["GET", "HEAD"], async (Guid id, HttpContext ctx, IEnumerable<IExternalMediaResolver> resolvers, ExternalMedia media, CancellationToken ct) =>
        {
            try {
                ExternalMediaSource? source = null;
                foreach (var resolver in resolvers) { source = await resolver.ResolveAsync(id, ct); if (source is not null) break; }
                if (source is null) throw new ExternalMediaException(404);
                if (!media.Allowed(source)) throw new ExternalMediaException(502);
                ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
                if (HttpMethods.IsHead(ctx.Request.Method)) {
                    using var cached = media.TryOpenCached(id);
                    if (cached is not null) { ctx.Response.ContentType = cached.ContentType; ctx.Response.ContentLength = cached.Stream.Length; ctx.Response.Headers.ETag = cached.ETag; }
                    ctx.Response.StatusCode = 200; return;
                }
                using var image = await media.OpenAsync(id, source, ct);
                ctx.Response.Headers.CacheControl = "public,max-age=3600";
                ctx.Response.Headers.ETag = image.ETag;
                if (image.ETag is not null && ctx.Request.Headers.IfNoneMatch.Any(x => x == image.ETag)) { ctx.Response.StatusCode = 304; return; }
                ctx.Response.ContentType = image.ContentType;
                await image.Stream.CopyToAsync(ctx.Response.Body, ct);
            } catch (ExternalMediaException ex) {
                if (ctx.Response.HasStarted) { ctx.Abort(); return; }
                ctx.Response.StatusCode = ex.Status;
                ctx.Response.Headers.CacheControl = "no-store";
                if (ex.Status == 503) ctx.Response.Headers.RetryAfter = "2";
                await ctx.Response.WriteAsJsonAsync(ApiResponseHelper.Error("تصویر در دسترس نیست.", statusCode: ex.Status), ct);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException) { ctx.Abort(); }
        }).WithName("Media.ExternalImage").WithTags("Media");
    }
}
