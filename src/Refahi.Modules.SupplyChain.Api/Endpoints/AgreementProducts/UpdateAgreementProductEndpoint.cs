using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementProducts;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.AgreementProducts;

public class UpdateAgreementProductEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/agreements/{id:guid}/products/{productId:guid}", async (
            Guid id,
            Guid productId,
            [FromBody] UpdateAgreementProductRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new UpdateAgreementProductCommand(
                id,
                productId,
                body.Name,
                body.Description,
                body.CategoryId,
                body.ProductType,
                body.DeliveryType,
                body.SalesModel,
                body.CommissionPercent);

            await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success<object>(null!, "محصول با موفقیت بروزرسانی شد"));
        })
        .WithName("SupplyChain.UpdateAgreementProduct")
        .WithTags("SupplyChain.AgreementProducts")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdateAgreementProductRequest(
    string Name,
    string? Description,
    int? CategoryId,
    short ProductType,
    short DeliveryType,
    short SalesModel,
    decimal CommissionPercent);
