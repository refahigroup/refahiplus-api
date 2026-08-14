using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Checkout;
using Refahi.Modules.Store.Application.Features.Checkout.PlaceStoreOrder;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Checkout;

public sealed class PlaceStoreOrderEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;
        routes
            .MapPost("/{moduleSlug}/checkout", HandleAsync)
            .WithName("Store.PlaceStoreOrder")
            .WithTags("Store.Checkout")
            .RequireAuthorization("UserOrAdmin")
            .Produces<ApiResponse<PlaceStoreOrderResponse>>(StatusCodes.Status200OK)
            .Produces<OfferChangedConflictResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> HandleAsync(
        string moduleSlug,
        [FromBody] CheckoutBody body,
        HttpContext context,
        IModuleResolver modules,
        IMediator mediator,
        CancellationToken ct
    )
    {
        if (
            !Guid.TryParse(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirstValue("sub"),
                out var userId
            )
        )
            return Results.Unauthorized();
        var key = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
            return Results.BadRequest(ApiResponseHelper.Error("هدر Idempotency-Key الزامی است"));
        var moduleId = await modules.ResolveIdAsync(moduleSlug, ct);
        if (!moduleId.HasValue)
            return Results.NotFound(
                ApiResponseHelper.Error("ماژول فروشگاه یافت نشد", statusCode: 404)
            );
        try
        {
            var result = await mediator.Send(
                new PlaceStoreOrderCommand(
                    userId,
                    moduleId.Value,
                    key,
                    body.ShippingAddressId,
                    body.DeliveryDate,
                    body.DeliveryTimeSlot,
                    body.CartItemDeliveryMethods
                ),
                ct
            );
            return Results.Ok(
                ApiResponseHelper.Success(result, "سفارش فروشگاه ثبت و برای پرداخت آماده شد")
            );
        }
        catch (OfferChangedException ex)
        {
            return Results.Conflict(
                new OfferChangedConflictResponse(
                    false,
                    "OFFER_CHANGED",
                    ex.Message,
                    ex.Details,
                    409
                )
            );
        }
        catch (StoreDomainException ex) when (ex.ErrorCode == "IDEMPOTENCY_PAYLOAD_MISMATCH")
        {
            return Results.Conflict(
                new IdempotencyConflictResponse(false, ex.ErrorCode, ex.Message, 409)
            );
        }
    }

    public sealed record CheckoutBody(
        Guid? ShippingAddressId,
        DateOnly? DeliveryDate,
        short DeliveryTimeSlot,
        Dictionary<Guid, short>? CartItemDeliveryMethods
    );

    public sealed record OfferChangedConflictResponse(
        bool Success,
        string Code,
        string Message,
        IReadOnlyList<OfferChangedDetail> Details,
        int StatusCode
    );

    public sealed record IdempotencyConflictResponse(
        bool Success,
        string Code,
        string Message,
        int StatusCode
    );
}
