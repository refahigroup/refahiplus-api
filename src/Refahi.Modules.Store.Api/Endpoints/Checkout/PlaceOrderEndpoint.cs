using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Checkout;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Store.Api.Endpoints.Checkout;

public class PlaceOrderEndpoint : IEndpoint
{
    /// <summary>
    /// Body endpoint — جداگانه از Command برای امکان پردازش راحت‌تر در Endpoint.
    /// </summary>
    public sealed record PlaceOrderRequestBody(
        List<WalletPaymentInput> WalletAllocations,
        Guid? ShippingAddressId,
        DateOnly? DeliveryDate,
        short DeliveryTimeSlot,
        Dictionary<Guid, short>? CartItemDeliveryMethods,
        string? DiscountCode);

    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/{moduleSlug}/checkout", async (
            string moduleSlug,
            [FromBody] PlaceOrderRequestBody body,
            HttpContext httpContext,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Results.BadRequest(ApiResponseHelper.Error("هدر Idempotency-Key الزامی است"));

            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var command = new PlaceStoreOrderCommand(
                UserId: userId,
                ModuleId: moduleId.Value,
                WalletAllocations: body.WalletAllocations ?? new List<WalletPaymentInput>(),
                IdempotencyKey: idempotencyKey,
                ShippingAddressId: body.ShippingAddressId,
                DeliveryDate: body.DeliveryDate,
                DeliveryTimeSlot: body.DeliveryTimeSlot,
                CartItemDeliveryMethods: body.CartItemDeliveryMethods,
                DiscountCode: body.DiscountCode);

            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "سفارش با موفقیت ثبت و پرداخت شد"));
        })
        .WithName("Store.PlaceOrder")
        .WithTags("Store.Checkout")
        .RequireAuthorization("UserOrAdmin")
        .Produces<ApiResponse<PlaceStoreOrderResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
