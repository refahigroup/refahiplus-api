using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Api.Models;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreatePaymentIntent;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Wallets.Api.Endpoints.Payments;

public class CreatePaymentIntentEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/payments/intents", async (
            CreatePaymentIntentBody request,
            HttpRequest httpRequest,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (!httpRequest.Headers.TryGetValue("Idempotency-Key", out var idemKeyValues))
                return Results.BadRequest(new ErrorResponse("MISSING_IDEMPOTENCY_KEY", "Missing Idempotency-Key header"));

            var idemKey = idemKeyValues.ToString();

            var command = new CreatePaymentIntentCommand(
                OrderId: request.OrderId,
                AmountMinor: request.AmountMinor,
                Currency: request.Currency,
                Allocations: request.Allocations,
                IdempotencyKey: idemKey,
                MetadataJson: request.MetadataJson);

            try
            {
                var resp = await mediator.Send(command, ct);

                if (resp.Status == CommandStatus.InProgress)
                    return Results.AcceptedAtRoute(
                        "CreatePaymentIntent", 
                        new { },
                        new ErrorResponse(
                            "IN_PROGRESS", 
                            "The payment intent reservation is currently pending. Retry later using the same Idempotency-Key."));

                return Results.Ok(resp.Data);
            }
            catch (WalletNotFoundException wnf)
            {
                return Results.NotFound(new ErrorResponse(wnf.Code, wnf.Message));
            }
            catch (InsufficientFundsException ife)
            {
                return Results.Conflict(new ErrorResponse(ife.Code, ife.Message));
            }
            catch (WalletCurrencyMismatchException wcm)
            {
                return Results.Conflict(new ErrorResponse(wcm.Code, wcm.Message));
            }
            catch (WalletOperationNotAllowedException woa)
            {
                return Results.Conflict(new ErrorResponse(woa.Code, woa.Message));
            }
            catch (IdempotencyKeyConflictException ikc)
            {
                return Results.Conflict(new ErrorResponse(ikc.Code, ikc.Message));
            }
            catch (ValidationException vex)
            {
                var errors = string.Join("; ", vex.Errors.Select(e => e.ErrorMessage));
                return Results.BadRequest(new ErrorResponse("VALIDATION_FAILED", errors));
            }
        })
        .RequireAuthorization()
        .WithName("CreatePaymentIntent")
        .WithTags("Wallets", "Payments")
        .Produces<CreatePaymentIntentResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status202Accepted)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .Produces<ErrorResponse>(StatusCodes.Status409Conflict);
    }

    public sealed record CreatePaymentIntentBody(
        Guid OrderId,
        long AmountMinor,
        string Currency,
        List<AllocationRequest> Allocations,
        string? MetadataJson);
}
