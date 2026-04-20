using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Reviews;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Store.Api.Endpoints.Reviews;

public class CreateReviewEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/{moduleSlug}/products/{productId:guid}/reviews", async (
            string moduleSlug,
            Guid productId,
            [FromBody] CreateReviewCommand command,
            HttpContext httpContext,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var adjustedCommand = command with { ProductId = productId, UserId = userId };
            var result = await mediator.Send(adjustedCommand, ct);
            return Results.Created(
                $"/{moduleSlug}/products/{productId}/reviews/{result.ReviewId}",
                ApiResponseHelper.Success(result, "نظر با موفقیت ثبت شد", 201));
        })
        .WithName("Store.CreateReview")
        .WithTags("Store.Reviews")
        .RequireAuthorization("UserOrAdmin")
        .Produces<ApiResponse<CreateReviewResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
