using System.Security.Claims;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Shared.Presentation;
namespace Refahi.Modules.Hotels.Api.EndPoints.Catalogue;

public sealed class HotelCatalogueEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        var v2 = routes.MapGroup("/v2").AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        v2.MapGet("/cities", async (string? name, ISender sender, CancellationToken ct) =>
            await Execute(async () => await sender.Send(new GetHotelCitiesQuery(name), ct)))
            .WithName("Hotels.V2.Cities").WithTags("Hotels");
        v2.MapGet("/availability/city", async (HttpRequest request, ISender sender, CancellationToken ct) =>
            await Execute(async () => await sender.Send(new SearchHotelCatalogueQuery(ParseGuid(request, "cityKey"), ParseInt(request, "cityId"), Travel(request),
                ParseLong(request, "minPrice"), ParseLong(request, "maxPrice"), request.Query["stars"].Select(x => int.Parse(x!)).ToArray(), request.Query["sort"].FirstOrDefault() ?? "price",
                request.Query["accommodations"].Select(x => x!).ToArray()), ct)))
            .WithName("Hotels.V2.Search").WithTags("Hotels");
        v2.MapGet("/{hotelKey:guid}", async (Guid hotelKey, HttpRequest request, ISender sender, CancellationToken ct) =>
            await Execute(async () => await sender.Send(new GetHotelCatalogueDetailsQuery(hotelKey, Travel(request)), ct)))
            .WithName("Hotels.V2.Details").WithTags("Hotels");
        v2.MapGet("/offers/{token}", async (string token, ISender sender, CancellationToken ct) =>
            await Execute(async () => await sender.Send(new GetHotelOfferQuery(token), ct)))
            .WithName("Hotels.V2.Offer").WithTags("Hotels");
        v2.MapPost("/requests", async (CreateHotelCatalogueRequestBody body, HttpContext context, ISender sender, CancellationToken ct) =>
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
            if (!Guid.TryParse(userId, out var id)) return Results.Json(ApiResponseHelper.Error("احراز هویت انجام نشده است.", statusCode: 401), statusCode: 401);
            return await Execute(async () => await sender.Send(new CreateHotelCatalogueRequestCommand(id, body.OfferToken, body.Travel,
                body.Guests, body.Phone, body.Email, body.ExpectedTotalPriceMinor, context.Request.Headers["Idempotency-Key"].ToString()), ct));
        }).RequireAuthorization("UserOrAdmin").WithName("Hotels.V2.CreateRequest").WithTags("Hotels");
        v2.MapPost("/offers/{token}/quote", async (string token, HotelTravel travel, ISender sender, CancellationToken ct) =>
            await Execute(async () => await sender.Send(new QuoteHotelOfferQuery(token, travel), ct)))
            .WithName("Hotels.V2.Quote").WithTags("Hotels");
    }
    private static async Task<IResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Results.Ok(ApiResponseHelper.Success(await action())); }
        catch (HotelCatalogueConflictException ex) { return Results.Json(ApiResponseHelper.Error(ex.Message, new() { ["code"] = ["OFFER_CONFLICT"] }, statusCode: 409), statusCode: 409); }
        catch (HotelExternalOutcomeUnknownException) { return Results.Json(ApiResponseHelper.Error("نتیجه رزرو در حال بررسی است. درخواست جدید ثبت نکنید.", new() { ["code"] = ["EXTERNAL_UNKNOWN"] }, statusCode: 409), statusCode: 409); }
        catch (Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CreateHotelRequest.HotelPriceChangedException ex)
        { return Results.Json(ApiResponseHelper.Error(ex.Message, statusCode: 409), statusCode: 409); }
        catch (HotelCatalogueUnavailableException ex) { return Results.Json(ApiResponseHelper.Error(ex.Message, statusCode: 503), statusCode: 503); }
        catch (FluentValidation.ValidationException ex)
        {
            var errors = ex.Errors.GroupBy(x => x.PropertyName).ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray());
            if (errors.Count == 0) errors["travel"] = ["شرایط اقامت و مسافران معتبر نیست."];
            return Results.BadRequest(ApiResponseHelper.ValidationError(errors));
        }
        catch (HttpRequestException) { return Results.Json(ApiResponseHelper.Error("سرویس هتل در دسترس نیست.", statusCode: 503), statusCode: 503); }
        catch (InvalidOperationException) { return Results.BadRequest(ApiResponseHelper.Error("درخواست هتل قابل انجام نیست؛ اطلاعات انتخاب را دوباره بررسی کنید.", statusCode: 400)); }
        catch (FormatException) { return Results.BadRequest(ApiResponseHelper.Error("پارامترهای جست‌وجو معتبر نیستند.", statusCode: 400)); }
        catch (JsonException) { return Results.BadRequest(ApiResponseHelper.Error("ترکیب اتاق‌ها معتبر نیست.", statusCode: 400)); }
    }
    private static Guid? ParseGuid(HttpRequest request, string name) => Guid.TryParse(request.Query[name], out var value) ? value : null;
    private static int? ParseInt(HttpRequest request, string name) => int.TryParse(request.Query[name], out var value) ? value : null;
    private static long? ParseLong(HttpRequest request, string name) => long.TryParse(request.Query[name], out var value) ? value : null;
    private static HotelTravel Travel(HttpRequest request)
    {
        var checkin = DateOnly.Parse(request.Query["checkin"].ToString()); var checkout = DateOnly.Parse(request.Query["checkout"].ToString());
        var rooms = JsonSerializer.Deserialize<List<HotelParty>>(request.Query["parties"].ToString(), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new FormatException();
        return new(checkin, checkout, rooms);
    }
}
public sealed record CreateHotelCatalogueRequestBody(string OfferToken, HotelTravel Travel, IReadOnlyList<HotelGuest> Guests,
    string Phone, string? Email, long ExpectedTotalPriceMinor);
