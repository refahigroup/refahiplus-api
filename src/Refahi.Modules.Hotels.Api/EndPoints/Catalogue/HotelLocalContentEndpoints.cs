using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Application.Features.Catalogue;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Hotels.Api.EndPoints.Catalogue;

public sealed class HotelLocalContentEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapGet("/v2/search", (HttpRequest request, IHotelLocalContentReader reader, IValidator<HotelLocalSearch> validator, CancellationToken ct) => Execute(async () => {
            var query = new HotelLocalSearch(request.Query["q"], Int(request, "referenceCityId"), Int(request, "provinceId"),
                request.Query["stars"].Select(x => int.Parse(x!, CultureInfo.InvariantCulture)).ToArray(), request.Query["accommodations"].Select(x => x!).ToArray(),
                request.Query["facilities"].Select(x => x!).ToArray(), Number(request, "minRating"), Number(request, "latitude"), Number(request, "longitude"),
                request.Query["sort"].FirstOrDefault() ?? "title", Int(request, "page") ?? 1, Int(request, "pageSize") ?? 20);
            await validator.ValidateAndThrowAsync(query, ct);
            return await reader.SearchAsync(query, ct); }))
            .WithName("Hotels.Local.Search").WithTags("Hotels");
        routes.MapGet("/v2/{hotelKey:guid}/reviews", (Guid hotelKey, HttpRequest request, IHotelLocalContentReader reader, CancellationToken ct) => Execute(async () =>
            await reader.ReviewsAsync(hotelKey, Int(request, "page") ?? 1, Int(request, "pageSize") ?? 20, ct)))
            .WithName("Hotels.Local.Reviews").WithTags("Hotels");
        routes.MapGet("/v2/legacy/{hotelId:long}/profile", (long hotelId, IHotelLocalContentReader reader, CancellationToken ct) => Execute(async () => await reader.LegacyProfileAsync(hotelId, ct)))
            .WithName("Hotels.Local.LegacyProfile").WithTags("Hotels");
        routes.MapGet("/v2/{hotelKey:guid}/availability", (Guid hotelKey, HttpRequest request, HotelCatalogueService service, CancellationToken ct) => Execute(async () =>
            await service.AvailabilityAsync(hotelKey, new(DateOnly.Parse(request.Query["checkin"].ToString(), CultureInfo.InvariantCulture), DateOnly.Parse(request.Query["checkout"].ToString(), CultureInfo.InvariantCulture),
                System.Text.Json.JsonSerializer.Deserialize<HotelParty[]>(request.Query["parties"].ToString(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? throw new FormatException()), ct)))
            .WithName("Hotels.Local.Availability").WithTags("Hotels");
        routes.MapGet("/v2/content/readiness", (IHotelLocalContentReader reader, CancellationToken ct) => Execute(async () => await reader.ReadinessAsync(ct)))
            .RequireAuthorization("AdminOnly").WithName("Hotels.Local.Readiness").WithTags("Hotels");
    }
    private static int? Int(HttpRequest request, string key) => request.Query.TryGetValue(key, out var value) ? int.Parse(value.ToString(), CultureInfo.InvariantCulture) : null;
    private static double? Number(HttpRequest request, string key) => request.Query.TryGetValue(key, out var value) ? double.Parse(value.ToString(), CultureInfo.InvariantCulture) : null;
    private static async Task<IResult> Execute<T>(Func<Task<T>> action)
    {
        try { var value = await action(); return value is null ? Results.NotFound(ApiResponseHelper.Error("هتل یافت نشد.", statusCode: 404)) : Results.Ok(ApiResponseHelper.Success(value)); }
        catch (Exception ex) when (ex is FormatException or OverflowException or System.Text.Json.JsonException or FluentValidation.ValidationException)
        { return Results.BadRequest(ApiResponseHelper.Error("پارامترهای درخواست معتبر نیستند.", statusCode: 400)); }
        catch (KeyNotFoundException) { return Results.NotFound(ApiResponseHelper.Error("هتل یافت نشد.", statusCode: 404)); }
        catch (Exception ex) when (ex is Refahi.Modules.Catalogue.Application.Contracts.CatalogueUnavailableException or HotelCatalogueUnavailableException or HttpRequestException or InvalidOperationException)
        { return Results.Json(ApiResponseHelper.Error("سرویس هتل در دسترس نیست.", statusCode: 503), statusCode: 503); }
    }
}
