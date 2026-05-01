using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.References.Application.Contracts.Commands;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.References.Api.Endpoints.Categories;

public class CreateCategoryEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/categories", async (
            [FromBody] CreateCategoryCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created(
                $"/references/categories/{result.Id}",
                ApiResponseHelper.Success(result, "دسته‌بندی با موفقیت ایجاد شد", 201));
        })
        .WithName("References.CreateCategory")
        .WithTags("References.Categories")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<CreateCategoryResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
