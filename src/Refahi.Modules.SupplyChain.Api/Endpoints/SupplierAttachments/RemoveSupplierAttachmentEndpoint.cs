using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierAttachments;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.SupplierAttachments;

public class RemoveSupplierAttachmentEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapDelete("/admin/suppliers/{id:guid}/attachments/{attachmentId:guid}", async (
            Guid id,
            Guid attachmentId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new RemoveSupplierAttachmentCommand(id, attachmentId), ct);
            return Results.NoContent();
        })
        .WithName("SupplyChain.RemoveSupplierAttachment")
        .WithTags("SupplyChain.SupplierAttachments")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
