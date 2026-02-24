using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability.AvailabilityByCity;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Hotels.Api.EndPoints.Availability;



public sealed class GetAvailabilityByCityEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/availability/city", async (
            [FromQuery] int cityId,
            [FromQuery] DateOnly checkin,
            [FromQuery] DateOnly checkout,
            [FromQuery] int? adults,
            [FromQuery] int? children,
            [FromQuery] int? availableRooms,
            [FromQuery] int? minPrice,
            [FromQuery] int? maxPrice,
            [FromQuery] int[]? stars,
            [FromQuery] string[]? accommodations,
            [FromQuery] string searchSource,
            ISender sender
        ) =>
        {
            var query = new GetAvailabilityByCityQuery(
                cityId,
                checkin,
                checkout,
                adults,
                children,
                availableRooms,
                minPrice,
                maxPrice,
                stars,
                accommodations
            );

            var result = await sender.Send(query);

            // Response will be automatically wrapped by ResponseWrappingMiddleware
            // No need to manually wrap it here
            return Results.Ok(result);
        })
        .Produces<ApiResponse<GetAvailabilityByCityDto>>()
        .WithName("Hotels.Availability.ByCity")
        .WithTags("Hotels");
    }
}