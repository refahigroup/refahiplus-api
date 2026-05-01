using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.Suppliers;

public class ChangeSupplierStatusEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPatch("/admin/suppliers/{id:guid}/status", async (
            Guid id,
            [FromBody] ChangeSupplierStatusRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new ChangeSupplierStatusCommand(id, body.NewStatus, body.Note);
            await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success<object>(null!, "وضعیت تامین‌کننده با موفقیت تغییر یافت"));
        })
        .WithName("SupplyChain.ChangeSupplierStatus")
        .WithTags("SupplyChain.Suppliers")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record ChangeSupplierStatusRequest(short NewStatus, string? Note);
