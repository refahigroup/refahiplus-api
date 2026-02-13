using Identity.Application.Features.Auth;
using Identity.Application.Features.Auth.Me;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Api.Endpoints;

public class MeEndpoint: IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        if (app == null)
            return;

        app.MapGet("/me", [Microsoft.AspNetCore.Authorization.Authorize] (System.Security.Claims.ClaimsPrincipal userPrincipal, IMediator mediator) =>
        {
            var id = userPrincipal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(id)) 
                return Results.Unauthorized();

            var user = mediator.Send(new MeQuery(id)).Result;

            if (user is null) 
                return Results.NotFound();

            return Results.Ok(user);

        })
        .WithName("Identity.Me")
        .WithTags("Identity")
        .RequireAuthorization()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
