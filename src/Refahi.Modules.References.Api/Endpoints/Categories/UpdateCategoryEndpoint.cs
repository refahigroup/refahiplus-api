using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.References.Application.Contracts.Commands;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.References.Api.Endpoints.Categories;

public class UpdateCategoryEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/categories/{id:int}", async (
            int id,
            [FromBody] UpdateCategoryRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new UpdateCategoryCommand(
                id,
                body.Name,
                body.ImageUrl,
                body.SortOrder,
                body.IsActive);

            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "دسته‌بندی با موفقیت بروزرسانی شد"));
        })
        .WithName("References.UpdateCategory")
        .WithTags("References.Categories")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<UpdateCategoryResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdateCategoryRequest(
    string Name,
    string? ImageUrl,
    int SortOrder,
    bool IsActive);
