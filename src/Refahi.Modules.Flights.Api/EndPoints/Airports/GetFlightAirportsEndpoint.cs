using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Flights.Application.Features.Airports.GetFlightAirports;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Flights.Api.EndPoints.Airports;

public sealed class GetFlightAirportsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/airports", async (
            [FromQuery(Name = "q")] string? query,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetFlightAirportsQuery(query), cancellationToken);

            return Results.Ok(ApiResponseHelper.Success(result, "فرودگاه‌ها با موفقیت دریافت شدند."));
        })
        .WithName("Flights.Airports")
        .WithTags("Flights")
        .Produces<ApiResponse<GetFlightAirportsResponse>>(StatusCodes.Status200OK);
    }
}
