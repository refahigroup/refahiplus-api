using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Orders.Api.Endpoints;

public record UpdateOrderStatusRequest(OrderStatusInput NewStatus);

public class UpdateOrderStatusEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/{orderId:guid}/status", async (
            Guid orderId,
            UpdateOrderStatusRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new UpdateOrderStatusCommand(orderId, request.NewStatus);
            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Orders.UpdateOrderStatus")
        .WithTags("Orders")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<UpdateOrderStatusResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
