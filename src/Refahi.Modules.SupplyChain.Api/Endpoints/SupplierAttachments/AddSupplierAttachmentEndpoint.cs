using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierAttachments;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.SupplierAttachments;

public class AddSupplierAttachmentEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/suppliers/{id:guid}/attachments", async (
            Guid id,
            [FromBody] AddSupplierAttachmentRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new AddSupplierAttachmentCommand(
                id, body.Title, body.FileUrl, body.FileName, body.ContentType, body.SizeBytes);

            var result = await mediator.Send(command, ct);
            return Results.Created(
                $"/api/supply-chain/admin/suppliers/{id}/attachments/{result.AttachmentId}",
                ApiResponseHelper.Success(result, "پیوست با موفقیت اضافه شد", 201));
        })
        .WithName("SupplyChain.AddSupplierAttachment")
        .WithTags("SupplyChain.SupplierAttachments")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<AddSupplierAttachmentResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record AddSupplierAttachmentRequest(
    string Title,
    string FileUrl,
    string? FileName,
    string? ContentType,
    long? SizeBytes);
