using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;
using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Refahi.Modules.Store.Api.Endpoints.Checkout;

public class SuggestAllocationsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/{moduleSlug}/checkout/suggest-allocations", async (
            string moduleSlug,
            [FromBody] SuggestAllocationsRequest request,
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

            var query = new SuggestWalletAllocationsQuery(
                UserId: userId,
                TotalAmountMinor: request.TotalAmountMinor,
                CartCategoryCodes: request.CartCategoryCodes ?? new List<string>());

            var result = await mediator.Send(query, ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.Checkout.SuggestAllocations")
        .WithTags("Store.Checkout")
        .RequireAuthorization("UserOrAdmin")
        .Produces<ApiResponse<SuggestAllocationsResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record SuggestAllocationsRequest(
    long TotalAmountMinor,
    List<string>? CartCategoryCodes = null);
