using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Media.Application.Contracts.DTOs;
using Refahi.Modules.Media.Application.Contracts.Queries;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Media.Api.Endpoints;

public class ListMediaAssetsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/", async (
            IMediator mediator,
            [FromQuery] int page = 1,
            [FromQuery] int size = 20,
            [FromQuery] string? entityType = null,
            [FromQuery] Guid? uploadedBy = null,
            CancellationToken ct = default) =>
        {
            var result = await mediator.Send(
                new ListMediaAssetsQuery(page, size, entityType, uploadedBy), ct);

            return Results.Ok(ApiResponseHelper.SuccessPaginated(
                result.Items,
                result.Page,
                result.Size,
                result.Total));
        })
        .WithName("Media.List")
        .WithTags("Media")
        .RequireAuthorization("AdminOnly")
        .Produces<PaginatedResponse<MediaAssetDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
