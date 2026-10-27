using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Media.Infrastructure.Services;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Media.Api.Endpoints;

public sealed class PersistentExternalMediaEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapMethods("/media/persistent/{id:guid}", ["GET", "HEAD"], (Guid id, HttpContext ctx, PersistentExternalMedia media) =>
        {
            var file = media.Open(id);
            if (file is null) return Results.NotFound(ApiResponseHelper.Error("تصویر در دسترس نیست.", statusCode: 404));
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers.CacheControl = "public,max-age=3600";
            if (HttpMethods.IsHead(ctx.Request.Method))
            {
                using (file) { ctx.Response.ContentType = "image/webp"; ctx.Response.ContentLength = file.Length; }
                return Results.Ok();
            }
            return Results.Stream(file, "image/webp");
        }).WithName("Media.PersistentImage").WithTags("Media");
    }
}
