using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Flights.Application.Features.Search;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Flights.Api.EndPoints.Search;

public sealed class SearchFlightsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/search", async (
            [FromQuery] string? origin,
            [FromQuery] string? destination,
            [FromQuery] string? departureDate,
            [FromQuery] string? returnDate,
            [FromQuery] int? adult,
            [FromQuery] int? child,
            [FromQuery] int? infant,
            [FromQuery] string? cabinType,
            [FromQuery] string? airTripType,
            [FromQuery] bool? isDomestic,
            [FromQuery] int? maxStopsQuantity,
            [FromQuery] string[]? vendorExcludeCodes,
            [FromQuery] string[]? vendorPreferenceCodes,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseDate(departureDate, out var parsedDepartureDate))
                return Results.BadRequest(ApiResponseHelper.Error("فرمت تاریخ رفت معتبر نیست."));

            if (!TryParseDate(returnDate, out var parsedReturnDate))
                return Results.BadRequest(ApiResponseHelper.Error("فرمت تاریخ برگشت معتبر نیست."));

            var query = new SearchFlightsQuery(
                origin,
                destination,
                parsedDepartureDate,
                parsedReturnDate,
                adult ?? 1,
                child ?? 0,
                infant ?? 0,
                string.IsNullOrWhiteSpace(cabinType) ? "Economy" : cabinType,
                airTripType,
                isDomestic,
                maxStopsQuantity,
                vendorExcludeCodes,
                vendorPreferenceCodes);

            var result = await sender.Send(query, cancellationToken);

            return Results.Ok(ApiResponseHelper.Success(result, "جست‌وجوی پرواز با موفقیت انجام شد."));
        })
        .WithName("Flights.Search")
        .WithTags("Flights")
        .Produces<ApiResponse<SearchFlightsResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
    }

    private static bool TryParseDate(string? value, out DateOnly? date)
    {
        date = null;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        date = parsed;
        return true;
    }
}
