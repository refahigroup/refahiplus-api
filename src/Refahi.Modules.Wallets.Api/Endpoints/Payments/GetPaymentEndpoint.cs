using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Api.Models;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Queries;
using Refahi.Modules.Wallets.Application.Contracts.Responses;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Wallets.Api.Endpoints.Payments;

/// <summary>
/// GET /api/wallets/payments/{paymentId}
/// Read-only query endpoint - NO writes, NO mutations.
/// </summary>
public sealed class GetPaymentEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/payments/{paymentId:guid}", async (
            [FromRoute] Guid paymentId,
            [FromServices] ISender mediator,
            CancellationToken ct) =>
        {
            try
            {
                var query = new GetPaymentQuery(paymentId);
                var result = await mediator.Send(query, ct);

                return result.Status == CommandStatus.Completed
                    ? Results.Ok(result.Data)
                    : Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
            catch (PaymentNotFoundException ex)
            {
                return Results.NotFound(new ErrorResponse(ex.Code, ex.Message));
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Internal server error",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .RequireAuthorization()
        .WithName("Payments.GetPayment")
        .WithTags("Payments")
        .Produces<GetPaymentResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status500InternalServerError);
    }
}
