using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Store.Api.Endpoints.Cart;

public class SyncCartEndpoint : IEndpoint
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/{moduleSlug}/cart/sync", async (
            string moduleSlug,
            [FromBody] SyncCartBody body,
            HttpContext httpContext,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Require Idempotency-Key header
            if (!httpContext.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var idempotencyKey)
                || string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return Results.BadRequest(ApiResponseHelper.Error("کلید همگام‌سازی الزامی است"));
            }

            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var command = new SyncCartCommand(
                userId,
                moduleId.Value,
                body.Items ?? [],
                idempotencyKey!);

            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.SyncCart")
        .WithTags("Store.Cart")
        .RequireAuthorization("UserOrAdmin")
        .Produces<ApiResponse<SyncCartResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}

/// <summary>Request body for sync endpoint — items from local cart</summary>
public sealed record SyncCartBody(IReadOnlyList<SyncCartItemInput>? Items);
