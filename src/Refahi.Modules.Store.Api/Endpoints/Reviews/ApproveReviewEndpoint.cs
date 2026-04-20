using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Reviews;
using Refahi.Shared.Presentation;
using System;

namespace Refahi.Modules.Store.Api.Endpoints.Reviews;

public class ApproveReviewEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/reviews/{reviewId:guid}/approve", async (
            Guid reviewId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new ApproveReviewCommand(reviewId), ct);
            return Results.Ok(ApiResponseHelper.Success(result, "نظر با موفقیت تایید شد"));
        })
        .WithName("Store.Admin.ApproveReview")
        .WithTags("Store.Reviews")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<ApproveReviewResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
