using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class CreateChargeRequestEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("charge-requests", async ([FromBody] CreateChargeRequestBody body, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!ChargeEndpointHelpers.TryUserId(http, out var userId))
                return Results.Unauthorized();

            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key))
                return Results.BadRequest(ApiResponseHelper.Error("هدر Idempotency-Key الزامی است"));

            try
            {
                var result = await sender.Send(new CreateChargeRequestCommand(
                    userId,
                    body.Operator,
                    body.ServiceType,
                    body.DestinationMobileNumber,
                    body.OriginMobileNumber,
                    body.ProviderProductId,
                    body.RequestedAmountMinor,
                    body.PinCategoryId,
                    body.PinCount,
                    body.ExpectedFinalAmountMinor,
                    key), ct);

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
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Charge.Requests.Create")
        .WithTags("Charge.Requests")
        .Produces<ApiResponse<CreateChargeRequestResponse>>(StatusCodes.Status201Created)
        .Produces<ApiResponse<ChargeRequestQuoteResponse>>(StatusCodes.Status409Conflict)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
