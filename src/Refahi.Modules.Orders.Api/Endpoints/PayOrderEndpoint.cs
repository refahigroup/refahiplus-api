using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Orders.Api.Endpoints;

public record PayOrderRequest(List<WalletAllocationInput> Allocations, string IdempotencyKey);

public class PayOrderEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/{orderId:guid}/pay", async (
            Guid orderId,
            PayOrderRequest request,
            HttpContext httpContext,
            ILogger<PayOrderEndpoint> logger,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (!TryGetUserId(httpContext, out var userId))
                return Results.Unauthorized();

            using var scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["RequestId"] = httpContext.TraceIdentifier,
                ["UserId"] = userId,
                ["SagaId"] = null,
                ["HotelRequestId"] = null,
                ["OrderId"] = orderId,
                ["ProviderBookingCode"] = null
            });

            var role = httpContext.User.IsInRole("Admin") ? "Admin" : "User";
            var command = new PayOrderCommand(orderId, userId, role, request.Allocations, request.IdempotencyKey);
            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Orders.PayOrder")
        .WithTags("Orders")
        .RequireAuthorization("UserOrAdmin")
        .Produces<ApiResponse<PayOrderResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static bool TryGetUserId(HttpContext httpContext, out Guid userId)
    {
        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub");

        return Guid.TryParse(userIdClaim, out userId);
    }
}
