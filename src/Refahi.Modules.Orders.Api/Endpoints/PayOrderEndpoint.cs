using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Shared.Presentation;

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
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new PayOrderCommand(orderId, request.Allocations, request.IdempotencyKey);
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
}
