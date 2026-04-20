using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.References.Application.Contracts.Commands;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.References.Api.Endpoints.Cities;

public class UpdateCityEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/cities/{id:int}", async (
            int id,
            [FromBody] UpdateCityCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (id != command.Id)
                return Results.BadRequest(ApiResponseHelper.Error("شناسه در URL و Body یکسان نیست"));

            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "شهر با موفقیت به‌روزرسانی شد"));
        })
        .WithName("References.UpdateCity")
        .WithTags("References.Cities")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<UpdateCityResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
