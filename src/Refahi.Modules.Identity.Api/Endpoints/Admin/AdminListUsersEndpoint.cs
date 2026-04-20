using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Admin.ListUsers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Identity.Api.Endpoints.Admin;

public class AdminListUsersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin/users", async (
            IMediator mediator,
            string? search,
            string? role,
            bool? isActive,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var result = await mediator.Send(
                new AdminListUsersQuery(search, role, isActive, pageNumber, pageSize), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("Identity.Admin.ListUsers")
        .WithTags("Identity.Admin")
        .Produces<ApiResponse<AdminUsersPagedResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
