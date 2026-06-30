using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CreateHotelRequest;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Hotels.Api.EndPoints.HotelRequests;

public sealed class CreateHotelRequestEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("hotel-requests", async (
            [FromBody] CreateHotelRequestRequest body,
            HttpContext httpContext,
            ILogger<CreateHotelRequestEndpoint> logger,
            ISender sender,
            CancellationToken ct) =>
        {
            if (!TryGetUserId(httpContext, out var userId))
                return Results.Unauthorized();

            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Results.BadRequest(ApiResponseHelper.Error("هدر Idempotency-Key الزامی است"));

            using var scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["RequestId"] = httpContext.TraceIdentifier,
                ["UserId"] = userId,
                ["SagaId"] = null,
                ["HotelRequestId"] = null,
                ["OrderId"] = null,
                ["ProviderBookingCode"] = null
            });

            var result = await sender.Send(new CreateHotelRequestCommand(
                userId,
                body.ProviderName,
                body.ProviderHotelId,
                body.ProviderRoomId,
                body.SearchCriteriaSnapshot,
                body.SelectedHotelSnapshot,
                body.SelectedRoomSnapshot,
                body.TotalPrice,
                body.Currency,
                body.Breakdown,
                body.Fees,
                body.GuestInfoSnapshot,
                idempotencyKey), ct);

            return Results.Ok(ApiResponseHelper.Success(result, "درخواست هتل ثبت شد"));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Hotels.HotelRequests.Create")
        .WithTags("Hotels.HotelRequests")
        .Produces<ApiResponse<CreateHotelRequestResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }

    private static bool TryGetUserId(HttpContext httpContext, out Guid userId)
    {
        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub");

        return Guid.TryParse(userIdClaim, out userId);
    }
}

public sealed class CreateHotelRequestRequest
{
    public string ProviderName { get; set; } = "SnappTrip";
    public long ProviderHotelId { get; set; }
    public long ProviderRoomId { get; set; }
    public string SearchCriteriaSnapshot { get; set; } = "{}";
    public string SelectedHotelSnapshot { get; set; } = "{}";
    public string SelectedRoomSnapshot { get; set; } = "{}";
    public long TotalPrice { get; set; }
    public string Currency { get; set; } = "IRR";
    public string Breakdown { get; set; } = "{}";
    public string? Fees { get; set; }
    public string GuestInfoSnapshot { get; set; } = "{}";
}
