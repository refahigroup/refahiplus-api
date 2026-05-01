using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.AgreementProducts;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.AgreementProducts;

public class AddAgreementProductEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/agreements/{id:guid}/products", async (
            Guid id,
            [FromBody] AddAgreementProductRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new AddAgreementProductCommand(
                id,
                body.Name,
                body.Description,
                body.CategoryId,
                body.ProductType,
                body.DeliveryType,
                body.SalesModel,
                body.CommissionPercent);

            var result = await mediator.Send(command, ct);
            return Results.Created(
                $"/api/supply-chain/admin/agreements/{id}/products/{result.ProductId}",
                ApiResponseHelper.Success(result, "محصول با موفقیت افزوده شد", 201));
        })
        .WithName("SupplyChain.AddAgreementProduct")
        .WithTags("SupplyChain.AgreementProducts")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<AddAgreementProductResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record AddAgreementProductRequest(
    string Name,
    string? Description,
    int? CategoryId,
    short ProductType,
    short DeliveryType,
    short SalesModel,
    decimal CommissionPercent);
