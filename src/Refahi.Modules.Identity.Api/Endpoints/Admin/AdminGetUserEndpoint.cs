using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Admin.GetUser;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Identity.Api.Endpoints.Admin;

public class AdminGetUserEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin/users/{userId:guid}", async (
            Guid userId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new AdminGetUserQuery(userId), ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(ApiResponseHelper.Success(result));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("Identity.Admin.GetUser")
        .WithTags("Identity.Admin")
        .Produces<ApiResponse<AdminUserDetailDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
