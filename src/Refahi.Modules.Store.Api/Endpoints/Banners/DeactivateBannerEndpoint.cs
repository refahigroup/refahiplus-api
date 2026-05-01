using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Banners;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Banners;

public class DeactivateBannerEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/banners/{id:int}/deactivate", async (
            int id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeactivateBannerCommand(id), ct);
            return Results.Ok(ApiResponseHelper.Success(result, "بنر با موفقیت غیرفعال شد"));
        })
        .WithName("Store.DeactivateBanner")
        .WithTags("Store.Banners")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<DeactivateBannerResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
