using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Admin.DisableUser;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Identity.Api.Endpoints.Admin;

public class DisableUserEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/users/{userId:guid}/disable", async (
            Guid userId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new DisableUserCommand(userId), ct);

            if (!result.Success)
                return Results.BadRequest(ApiResponseHelper.Error(result.ErrorMessage ?? "خطا در غیرفعال‌سازی کاربر"));

            return Results.Ok(ApiResponseHelper.Success(true, "کاربر با موفقیت غیرفعال شد"));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("Identity.Admin.DisableUser")
        .WithTags("Identity.Admin")
        .Produces(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
