using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.PaymentGateway.Api.Models;
using Refahi.Modules.PaymentGateway.Application.Contracts.Exceptions;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;
using Refahi.Modules.PaymentGateway.Domain.Exceptions;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.PaymentGateway.Api.Endpoints;

/// <summary>
/// POST /api/payment-gateway/initiate
/// شروع فرآیند شارژ کیف‌پول از طریق درگاه پرداخت
/// </summary>
public class InitiatePaymentEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/initiate", async (
            InitiatePaymentRequestBody body,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            // Build the absolute callback URL where the provider will POST the result
            var req = httpContext.Request;
            var providerSlug = body.Provider.ToString().ToLowerInvariant();
            var providerCallbackUrl = $"{req.Scheme}://{req.Host}/api/payment-gateway/callback/{providerSlug}";

            var command = new InitiatePaymentCommand(
                UserId: userId,
                WalletId: body.WalletId,
                AmountMinor: body.AmountMinor,
                Currency: "IRR",
                Provider: body.Provider,
                ProviderCallbackUrl: providerCallbackUrl,
                ReturnBaseUrl: body.ReturnBaseUrl,
                SucceededCallbackUrl: body.SucceededCallbackUrl,
                FailedCallbackUrl: body.FailedCallbackUrl);

            try
            {
                var response = await mediator.Send(command, ct);
                return Results.Ok(ApiResponseHelper.Success(response));
            }
            catch (ValidationException ex)
            {
                var errors = ex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                return Results.BadRequest(ApiResponseHelper.ValidationError(errors));
            }
            catch (InvalidPaymentAmountException ex)
            {
                return Results.BadRequest(ApiResponseHelper.Error(ex.Message));
            }
            catch (PaymentTokenRequestFailedException ex)
            {
                return Results.BadRequest(ApiResponseHelper.Error(ex.Message));
            }
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("PaymentGateway.Initiate")
        .WithTags("PaymentGateway")
        .Produces<ApiResponse<InitiatePaymentResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
