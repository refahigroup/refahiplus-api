using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Admin.EditUser;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Identity.Api.Endpoints.Admin;

public class AdminEditUserEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/users/{userId:guid}", async (
            Guid userId,
            [FromBody] AdminEditUserRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new AdminEditUserCommand(userId, request.FirstName, request.LastName, request.NationalCode);
            var result = await mediator.Send(command, ct);

            if (!result.Success)
                return Results.BadRequest(ApiResponseHelper.Error(result.ErrorMessage ?? "خطا در ویرایش کاربر"));

            return Results.Ok(ApiResponseHelper.Success(true, "اطلاعات کاربر با موفقیت ویرایش شد"));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("Identity.Admin.EditUser")
        .WithTags("Identity.Admin")
        .Produces(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

public sealed record AdminEditUserRequest(string FirstName, string LastName, string? NationalCode = null);
