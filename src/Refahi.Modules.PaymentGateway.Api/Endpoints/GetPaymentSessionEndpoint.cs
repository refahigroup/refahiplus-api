using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.GetPaymentSession;
using Refahi.Shared.Presentation;
using System;
using System.Security.Claims;

namespace Refahi.Modules.PaymentGateway.Api.Endpoints;

/// <summary>
/// GET /api/payment-gateway/sessions/{sessionId}
/// نمایش وضعیت یک جلسه پرداخت — فقط برای مالک جلسه قابل دسترس است
/// </summary>
public class GetPaymentSessionEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/sessions/{sessionId:guid}", async (
            Guid sessionId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var query = new GetPaymentSessionQuery(sessionId, userId);
            var result = await mediator.Send(query, ct);

            if (result is null)
                return Results.NotFound();

            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("PaymentGateway.GetSession")
        .WithTags("PaymentGateway")
        .Produces<ApiResponse<PaymentSessionDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
