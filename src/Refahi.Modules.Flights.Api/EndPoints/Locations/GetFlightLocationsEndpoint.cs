using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Flights.Application.Features.Locations;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Flights.Api.EndPoints.Locations;

public sealed class GetFlightLocationsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapGet("/locations", async (
            [FromQuery] bool isDomestic, [FromQuery(Name = "q")] string? query,
            [FromQuery] int? limit, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFlightLocationsQuery(isDomestic, query, limit ?? 20), ct);
            return Results.Ok(ApiResponseHelper.Success(result, "مقصدهای پرواز دریافت شدند."));
        })
        .WithName("Flights.Locations").WithTags("Flights")
        .Produces<ApiResponse<GetFlightLocationsResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
    }
}

