using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Api.Models;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Wallets.Api.Endpoints.Payments;

public class ReleasePaymentIntentEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/payments/intents/{intentId:guid}/release", async (
            Guid intentId,
            HttpRequest httpRequest,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (!httpRequest.Headers.TryGetValue("Idempotency-Key", out var idemKeyValues))
                return Results.BadRequest(new ErrorResponse("MISSING_IDEMPOTENCY_KEY", "Missing Idempotency-Key header"));

            var idemKey = idemKeyValues.ToString();

            var command = new ReleasePaymentIntentCommand(
                IntentId: intentId,
                IdempotencyKey: idemKey);

            try
            {
                var resp = await mediator.Send(command, ct);

                if (resp.Status == CommandStatus.InProgress)
                    return Results.AcceptedAtRoute(
                        "ReleasePaymentIntent", 
                        new { intentId },
                        new ErrorResponse(
                            "IN_PROGRESS", 
                            "The payment release is currently pending. Retry later using the same Idempotency-Key."));

                return Results.Ok(resp.Data);
            }
            catch (PaymentIntentNotFoundException pnf)
            {
                return Results.NotFound(new ErrorResponse(pnf.Code, pnf.Message));
            }
            catch (PaymentIntentStateViolationException psv)
            {
                return Results.Conflict(new ErrorResponse(psv.Code, psv.Message));
            }
            catch (ValidationException vex)
            {
                var errors = string.Join("; ", vex.Errors.Select(e => e.ErrorMessage));
                return Results.BadRequest(new ErrorResponse("VALIDATION_FAILED", errors));
            }
        })
        .RequireAuthorization()
        .WithName("ReleasePaymentIntent")
        .WithTags("Wallets", "Payments")
        .Produces<ReleasePaymentIntentResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status202Accepted)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .Produces<ErrorResponse>(StatusCodes.Status409Conflict);
    }
}
