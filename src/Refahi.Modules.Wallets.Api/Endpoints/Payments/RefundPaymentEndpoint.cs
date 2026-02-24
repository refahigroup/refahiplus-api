using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Api;
using Refahi.Modules.Wallets.Api.Models;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Shared.Presentation;
using System.Text.Json;

namespace Refahi.Modules.Wallets.Api.Endpoints.Payments;

public class RefundPaymentEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/payments/{paymentId:guid}/refunds", async (
            Guid paymentId,
            HttpRequest httpRequest,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (!httpRequest.Headers.TryGetValue("Idempotency-Key", out var idemKeyValues))
                return Results.BadRequest(new ErrorResponse("MISSING_IDEMPOTENCY_KEY", "Missing Idempotency-Key header"));

            var idemKey = idemKeyValues.ToString();

            // Parse request body
            RefundPaymentRequestBody? body = null;
            try
            {
                body = await JsonSerializer.DeserializeAsync<RefundPaymentRequestBody>(
                    httpRequest.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    ct);
            }
            catch
            {
                return Results.BadRequest(new ErrorResponse("INVALID_JSON", "Invalid JSON body."));
            }

            var command = new RefundPaymentCommand(
                PaymentId: paymentId,
                IdempotencyKey: idemKey,
                Reason: body?.Reason,
                MetadataJson: body?.MetadataJson);

            try
            {
                var resp = await mediator.Send(command, ct);

                if (resp.Status == CommandStatus.InProgress)
                    return Results.AcceptedAtRoute(
                        "RefundPayment",
                        new { paymentId },
                        new ErrorResponse(
                            "IN_PROGRESS",
                            "The refund is currently pending. Retry later using the same Idempotency-Key."));

                return Results.Ok(resp.Data);
            }
            catch (PaymentNotFoundException pnf)
            {
                return Results.NotFound(new ErrorResponse(pnf.Code, pnf.Message));
            }
            catch (PaymentNotRefundableException pnr)
            {
                return Results.Conflict(new ErrorResponse(pnr.Code, pnr.Message));
            }
            catch (PaymentAlreadyRefundedException par)
            {
                return Results.Conflict(new ErrorResponse(par.Code, par.Message));
            }
            catch (ValidationException vex)
            {
                var errors = string.Join("; ", vex.Errors.Select(e => e.ErrorMessage));
                return Results.BadRequest(new ErrorResponse("VALIDATION_FAILED", errors));
            }
        })
        .RequireAuthorization()
        .WithName("RefundPayment")
        .WithTags("Wallets", "Payments")
        .Produces<RefundPaymentResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status202Accepted)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .Produces<ErrorResponse>(StatusCodes.Status409Conflict);
    }
}

/// <summary>
/// Request body for refund endpoint.
/// </summary>
public sealed record RefundPaymentRequestBody(
    string? Reason,
    string? MetadataJson);
