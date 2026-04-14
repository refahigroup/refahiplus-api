using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Banners;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Banners;

public class UpdateBannerEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/banners/{id:int}", async (
            int id,
            [FromBody] UpdateBannerCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var adjustedCommand = command with { Id = id };
            var result = await mediator.Send(adjustedCommand, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "بنر با موفقیت به‌روزرسانی شد"));
        })
        .WithName("Store.UpdateBanner")
        .WithTags("Store.Banners")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<UpdateBannerResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
