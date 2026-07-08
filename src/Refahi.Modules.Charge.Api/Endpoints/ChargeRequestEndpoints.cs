using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class ChargeRequestEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder r) return;
        r.MapPost("charge-requests", async ([FromBody] CreateChargeRequestBody body, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!ChargeEndpointHelpers.TryUserId(http, out var userId)) return Results.Unauthorized();
            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(ApiResponseHelper.Error("هدر Idempotency-Key الزامی است"));
            try
            {
                var result = await sender.Send(new CreateChargeRequestCommand(userId, body.Operator, body.ServiceType,
                    body.DestinationMobileNumber, body.OriginMobileNumber, body.ProviderProductId, body.RequestedAmountMinor,
                    body.PinCategoryId, body.PinCount, body.ExpectedFinalAmountMinor, key), ct);
                return Results.Created(
                    $"/api/charge/charge-requests/{result.RequestId}",
                    ApiResponseHelper.Success(result, "درخواست شارژ ثبت شد", StatusCodes.Status201Created));
            }
            catch (ChargeQuoteChangedException ex)
            {
                return Results.Conflict(new ApiResponse<ChargeRequestQuoteResponse>(
                    false,
                    ex.Quote,
                    ex.Message,
                    StatusCodes.Status409Conflict));
            }
        }).RequireAuthorization("UserOrAdmin").WithName("Charge.Requests.Create").WithTags("Charge.Requests")
            .Produces<ApiResponse<CreateChargeRequestResponse>>(StatusCodes.Status201Created)
            .Produces<ApiResponse<ChargeRequestQuoteResponse>>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        r.MapPost("charge-requests/{requestId:guid}/order", async (Guid requestId, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!ChargeEndpointHelpers.TryUserId(http, out var userId)) return Results.Unauthorized();
            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key)) return Results.BadRequest(ApiResponseHelper.Error("هدر Idempotency-Key الزامی است"));
            var result = await sender.Send(new ConvertChargeRequestToOrderCommand(requestId, userId, key), ct);
            return Results.Ok(ApiResponseHelper.Success(result, "سفارش شارژ آماده پرداخت شد"));
        }).RequireAuthorization("UserOrAdmin").WithName("Charge.Requests.ConvertToOrder").WithTags("Charge.Requests")
            .Produces<ApiResponse<ConvertChargeRequestToOrderResponse>>(StatusCodes.Status200OK).Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized);

        r.MapGet("charge-requests/{requestId:guid}", async (Guid requestId, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!ChargeEndpointHelpers.TryUserId(http, out var userId)) return Results.Unauthorized();
            var result = await sender.Send(new GetChargeRequestQuery(requestId, userId), ct);
            return result is null ? Results.NotFound(ApiResponseHelper.Error("درخواست شارژ یافت نشد", statusCode: 404))
                : Results.Ok(ApiResponseHelper.Success(result));
        }).RequireAuthorization("UserOrAdmin").WithName("Charge.Requests.Get").WithTags("Charge.Requests")
            .Produces<ApiResponse<ChargeRequestDetailDto>>(StatusCodes.Status200OK).Produces(StatusCodes.Status401Unauthorized).Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }
}
public sealed record CreateChargeRequestBody(ChargeOperator Operator, ChargeServiceType ServiceType,
    string DestinationMobileNumber, string? OriginMobileNumber, string? ProviderProductId,
    long? RequestedAmountMinor, int? PinCategoryId, long ExpectedFinalAmountMinor, int PinCount = 1);
