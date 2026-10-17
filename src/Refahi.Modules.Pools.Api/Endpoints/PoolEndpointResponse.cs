using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Shared.Presentation;
namespace Refahi.Modules.Pools.Api.Endpoints;
internal static class PoolEndpointResponse
{
    public static Guid User(HttpContext h) => Guid.TryParse(h.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? h.User.FindFirstValue("sub"), out var id) && id != Guid.Empty
        ? id : throw new PoolException("ابتدا وارد شوید", 401);
    public static void NoStore(HttpContext h) => h.Response.Headers.CacheControl = "private, no-store";
    public static RouteHandlerBuilder Named(RouteHandlerBuilder b, string name) => b.WithName("Pools." + name).WithTags("Pools");
    public static RouteHandlerBuilder Catalog(RouteHandlerBuilder b, string name) => Named(b, name).AllowAnonymous().AddEndpointFilter(async (context, next) =>
        context.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<PoolsOptions>>().CurrentValue.CatalogEnabled
            ? await next(context) : Error(new PoolException("سرویس استخر فعال نیست", 503, "CATALOG_DISABLED")));
    public static IResult Error(PoolException ex)
    {
        var response = ApiResponseHelper.Error(ex.Message, statusCode: ex.Status);
        return Results.Json(new { response.Success, response.Message, response.StatusCode, response.Errors, response.TraceId, Code = ex.Code, Details = ex.Details }, statusCode: ex.Status);
    }
    public static async Task<IResult> Run<T>(Func<Task<T>> work)
    {
        try { return Results.Ok(ApiResponseHelper.Success(await work())); }
        catch (PoolException ex) { return Error(ex); }
        catch (ValidationException ex) { return Results.Json(ApiResponseHelper.ValidationError(ex.Errors.GroupBy(x => x.PropertyName).ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray())), statusCode: 400); }
        catch (InvalidOperationException) { return Error(new PoolException("عملیات در وضعیت فعلی مجاز نیست", 409, "STATE_CONFLICT")); }
    }
}
