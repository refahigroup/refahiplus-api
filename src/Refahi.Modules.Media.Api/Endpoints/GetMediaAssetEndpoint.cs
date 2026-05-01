using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Media.Application.Contracts.DTOs;
using Refahi.Modules.Media.Application.Contracts.Queries;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Media.Api.Endpoints;

public class GetMediaAssetEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/{id:guid}", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetMediaAssetQuery(id), ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Media.GetById")
        .WithTags("Media")
        .Produces<ApiResponse<MediaAssetDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}
