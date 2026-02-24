using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Hotels.Application.Contracts.Services.Statics.Cities;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Hotels.Api.EndPoints.Statics;

public sealed class GetCitiesEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("statics/cities", async ([FromQuery] string? name, ISender sender) =>
        {
            var request = new GetCitiesRequest(name);

            var result = await sender.Send(request);

            return Results.Ok(result);

        })
        .Produces<IEnumerable<GetCitiesResponse>>()
        .WithName("Hotels.Statics.Cities")
        .WithTags("Hotels");
    }
}