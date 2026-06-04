using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Hotels.Application.Contracts.Services.Bookings.StartPayment;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Hotels.Api.EndPoints.Bookings;

public sealed class CreateHotelPaymentSessionEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("bookings/{bookingId:guid}/payment-session", async (
            Guid bookingId,
            [FromBody] CreateHotelPaymentSessionRequest body,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var bookingPayment = await sender.Send(new StartHotelBookingPaymentCommand(bookingId), ct);

            var req = httpContext.Request;
            var provider = body.Provider == 0 ? PaymentGatewayProviderType.Sep : body.Provider;
            var providerSlug = provider.ToString().ToLowerInvariant();
            var providerCallbackUrl = $"{req.Scheme}://{req.Host}/api/payment-gateway/callback/{providerSlug}";

            var successUrl = body.SucceededCallbackUrl
                ?? $"{body.ReturnBaseUrl.TrimEnd('/')}/{bookingId}/success";
            var failedUrl = body.FailedCallbackUrl
                ?? $"{body.ReturnBaseUrl.TrimEnd('/')}/{bookingId}/failed";

            var payment = await sender.Send(new InitiatePaymentCommand(
                UserId: userId,
                WalletId: body.WalletId,
                AmountMinor: bookingPayment.AmountMinor,
                Currency: "IRR",
                Provider: provider,
                ProviderCallbackUrl: providerCallbackUrl,
                ReturnBaseUrl: body.ReturnBaseUrl,
                SucceededCallbackUrl: successUrl,
                FailedCallbackUrl: failedUrl), ct);

            return Results.Ok(new CreateHotelPaymentSessionResponse
            {
                BookingId = bookingId,
                SessionId = payment.SessionId,
                GatewayRedirectUrl = payment.GatewayRedirectUrl,
                AmountMinor = bookingPayment.AmountMinor
            });
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Hotels.Bookings.PaymentSession")
        .WithTags("Bookings")
        .Produces<CreateHotelPaymentSessionResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}

public sealed class CreateHotelPaymentSessionRequest
{
    public Guid WalletId { get; set; }
    public PaymentGatewayProviderType Provider { get; set; } = PaymentGatewayProviderType.Sep;
    public string ReturnBaseUrl { get; set; } = "";
    public string? SucceededCallbackUrl { get; set; }
    public string? FailedCallbackUrl { get; set; }
}

public sealed class CreateHotelPaymentSessionResponse
{
    public Guid BookingId { get; set; }
    public Guid SessionId { get; set; }
    public string GatewayRedirectUrl { get; set; } = "";
    public long AmountMinor { get; set; }
}

