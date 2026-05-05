using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Auth.Me;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Identity.Api.Endpoints.Profile;

public class MeEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/me", [Microsoft.AspNetCore.Authorization.Authorize] async (System.Security.Claims.ClaimsPrincipal userPrincipal, IMediator mediator) =>
        {
            var id = userPrincipal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out var userId))
                return Results.Unauthorized();

            var me = await mediator.Send(new MeQuery(userId));

            if (me is null)
                return Results.NotFound();

            return Results.Ok(ApiResponseHelper.Success(me, "اطلاعات کاربر با موفقیت دریافت شد"));
        })
        .WithName("Identity.Me")
        .WithTags("Identity")
        .RequireAuthorization()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}

