using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Application.Contracts.Queries.Cart;
using Refahi.Modules.Store.Application.Features.Cart.OfferCartV3;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Cart;

public sealed class OfferCartV3Endpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;
        routes
            .MapPost("/v3/{moduleSlug}/cart/items", AddAsync)
            .WithName("Store.V3.AddOfferToCart")
            .WithTags("Store.Cart.V3")
            .RequireAuthorization("UserOrAdmin")
            .RequireRateLimiting("StoreCart")
            .Produces<ApiResponse<AddOfferToCartResponse>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
        routes
            .MapGet("/v3/{moduleSlug}/cart", GetAsync)
            .WithName("Store.V3.GetOfferCart")
            .WithTags("Store.Cart.V3")
            .RequireAuthorization("UserOrAdmin")
            .RequireRateLimiting("StoreCart")
            .Produces<ApiResponse<OfferCartDto>>(StatusCodes.Status200OK);
        routes
            .MapPut("/v3/{moduleSlug}/cart/items/{cartItemId:guid}", UpdateAsync)
            .WithName("Store.V3.UpdateOfferCartItem")
            .WithTags("Store.Cart.V3")
            .RequireAuthorization("UserOrAdmin")
            .RequireRateLimiting("StoreCart")
            .Produces<ApiResponse<OfferCartDto>>(StatusCodes.Status200OK)
            .Produces<CartOfferChangedConflictResponse>(StatusCodes.Status409Conflict);
        routes
            .MapDelete("/v3/{moduleSlug}/cart/items/{cartItemId:guid}", RemoveAsync)
            .WithName("Store.V3.RemoveOfferCartItem")
            .WithTags("Store.Cart.V3")
            .RequireAuthorization("UserOrAdmin")
            .RequireRateLimiting("StoreCart")
            .Produces<ApiResponse<OfferCartDto>>(StatusCodes.Status200OK);
        routes
            .MapPost("/v3/{moduleSlug}/cart/sync", SyncAsync)
            .WithName("Store.V3.SyncOfferCart")
            .WithTags("Store.Cart.V3")
            .RequireAuthorization("UserOrAdmin")
            .RequireRateLimiting("StoreCart")
            .Produces<ApiResponse<OfferCartDto>>(StatusCodes.Status200OK)
            .Produces<CartOfferChangedConflictResponse>(StatusCodes.Status409Conflict)
            .Produces<CartIdempotencyConflictResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);
        routes
            .MapPost("/v3/{moduleSlug}/cart/reconfirm", ReconfirmAsync)
            .WithName("Store.V3.ReconfirmOfferCart")
            .WithTags("Store.Cart.V3")
            .RequireAuthorization("UserOrAdmin")
            .RequireRateLimiting("StoreCart")
            .Produces<ApiResponse<OfferCartDto>>(StatusCodes.Status200OK)
            .Produces<CartOfferChangedConflictResponse>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> UpdateAsync(
        string moduleSlug,
        Guid cartItemId,
        [FromBody] UpdateOfferCartBody body,
        HttpContext context,
        IModuleResolver modules,
        IMediator mediator,
        CancellationToken ct
    )
    {
        if (!TryUser(context, out var userId))
            return Results.Unauthorized();
        var moduleId = await modules.ResolveIdAsync(moduleSlug, ct);
        if (!moduleId.HasValue)
            return ModuleNotFound();
        try
        {
            var result = await mediator.Send(
                new UpdateOfferCartItemCommand(
                    userId,
                    moduleId.Value,
                    cartItemId,
                    body.Quantity,
                    body.AcceptOfferChanges
                ),
                ct
            );
            return Results.Ok(ApiResponseHelper.Success(result, "سبد خرید به‌روزرسانی شد"));
        }
        catch (CartOfferChangedException ex)
        {
            return OfferChanged(ex);
        }
    }

    private static async Task<IResult> RemoveAsync(
        string moduleSlug,
        Guid cartItemId,
        HttpContext context,
        IModuleResolver modules,
        IMediator mediator,
        CancellationToken ct
    )
    {
        if (!TryUser(context, out var userId))
            return Results.Unauthorized();
        var moduleId = await modules.ResolveIdAsync(moduleSlug, ct);
        if (!moduleId.HasValue)
            return ModuleNotFound();
        var result = await mediator.Send(
            new RemoveOfferCartItemCommand(userId, moduleId.Value, cartItemId),
            ct
        );
        return Results.Ok(ApiResponseHelper.Success(result, "آیتم از سبد خرید حذف شد"));
    }

    private static async Task<IResult> SyncAsync(
        string moduleSlug,
        [FromBody] SyncOfferCartBody body,
        HttpContext context,
        IModuleResolver modules,
        IMediator mediator,
        CancellationToken ct
    )
    {
        if (!TryUser(context, out var userId))
            return Results.Unauthorized();
        var key = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
            return Results.BadRequest(ApiResponseHelper.Error("هدر Idempotency-Key الزامی است"));
        var moduleId = await modules.ResolveIdAsync(moduleSlug, ct);
        if (!moduleId.HasValue)
            return ModuleNotFound();
        try
        {
            var result = await mediator.Send(
                new SyncOfferCartCommand(
                    userId,
                    moduleId.Value,
                    key,
                    body.Items ?? [],
                    body.AcceptOfferChanges
                ),
                ct
            );
            return Results.Ok(
                ApiResponseHelper.Success(result, "سبد ناشناس با حساب کاربری همگام شد")
            );
        }
        catch (CartOfferChangedException ex)
        {
            return OfferChanged(ex);
        }
        catch (StoreDomainException ex) when (ex.ErrorCode == "IDEMPOTENCY_PAYLOAD_MISMATCH")
        {
            return Results.Conflict(
                new CartIdempotencyConflictResponse(false, ex.ErrorCode, ex.Message, 409)
            );
        }
    }

    private static async Task<IResult> ReconfirmAsync(
        string moduleSlug,
        HttpContext context,
        IModuleResolver modules,
        IMediator mediator,
        CancellationToken ct
    )
    {
        if (!TryUser(context, out var userId))
            return Results.Unauthorized();
        var moduleId = await modules.ResolveIdAsync(moduleSlug, ct);
        if (!moduleId.HasValue)
            return ModuleNotFound();
        try
        {
            var result = await mediator.Send(
                new ReconfirmOfferCartCommand(userId, moduleId.Value),
                ct
            );
            return Results.Ok(ApiResponseHelper.Success(result, "تغییرات پیشنهادهای سبد تأیید شد"));
        }
        catch (CartOfferChangedException ex)
        {
            return OfferChanged(ex);
        }
    }

    private static IResult OfferChanged(CartOfferChangedException ex) =>
        Results.Conflict(
            new CartOfferChangedConflictResponse(false, ex.ErrorCode, ex.Message, ex.Details, 409)
        );

    private static IResult ModuleNotFound() =>
        Results.NotFound(ApiResponseHelper.Error("ماژول فروشگاه یافت نشد", statusCode: 404));

    private static async Task<IResult> AddAsync(
        string moduleSlug,
        [FromBody] AddOfferCartBody body,
        HttpContext context,
        IModuleResolver modules,
        IMediator mediator,
        CancellationToken ct
    )
    {
        if (!TryUser(context, out var userId))
            return Results.Unauthorized();
        var moduleId = await modules.ResolveIdAsync(moduleSlug, ct);
        if (!moduleId.HasValue)
            return Results.NotFound(
                ApiResponseHelper.Error("ماژول فروشگاه یافت نشد", statusCode: 404)
            );
        var result = await mediator.Send(
            new AddOfferToCartCommand(
                userId,
                moduleId.Value,
                body.OfferId,
                body.Quantity,
                body.ProductVariantId,
                body.ProductSessionId,
                body.UsageDate
            ),
            ct
        );
        return Results.Ok(ApiResponseHelper.Success(result, "پیشنهاد به سبد خرید اضافه شد"));
    }

    private static async Task<IResult> GetAsync(
        string moduleSlug,
        HttpContext context,
        IModuleResolver modules,
        IMediator mediator,
        CancellationToken ct
    )
    {
        if (!TryUser(context, out var userId))
            return Results.Unauthorized();
        var moduleId = await modules.ResolveIdAsync(moduleSlug, ct);
        if (!moduleId.HasValue)
            return Results.NotFound(
                ApiResponseHelper.Error("ماژول فروشگاه یافت نشد", statusCode: 404)
            );
        var result = await mediator.Send(new GetOfferCartQuery(userId, moduleId.Value), ct);
        return result is null
            ? Results.NotFound(ApiResponseHelper.Error("سبد خرید یافت نشد", statusCode: 404))
            : Results.Ok(ApiResponseHelper.Success(result));
    }

    private static bool TryUser(HttpContext context, out Guid userId) =>
        Guid.TryParse(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub"),
            out userId
        );

    public sealed record AddOfferCartBody(
        Guid OfferId,
        int Quantity,
        Guid? ProductVariantId,
        Guid? ProductSessionId,
        DateOnly? UsageDate
    );

    public sealed record UpdateOfferCartBody(int Quantity, bool AcceptOfferChanges = false);

    public sealed record SyncOfferCartBody(
        IReadOnlyList<SyncOfferCartItemInput>? Items,
        bool AcceptOfferChanges = false
    );
}
