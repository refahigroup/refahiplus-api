using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.GetMyPaymentSessions;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.GetPaymentSession;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.PaymentGateway.Api.Endpoints;

public sealed class GetMyPaymentSessionsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/my-sessions", async (
            HttpContext httpContext,
            [FromQuery] int? take,
            [FromQuery] PaymentSessionStatus? status,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var result = await mediator.Send(new GetMyPaymentSessionsQuery(
                userId,
                take.GetValueOrDefault(20),
                status), ct);

            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("PaymentGateway.GetMySessions")
        .WithTags("PaymentGateway")
        .Produces<ApiResponse<IReadOnlyList<PaymentSessionDto>>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
