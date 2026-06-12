using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Flights.Application.Features.Offers;
using Refahi.Modules.Flights.Application.Features.Offers.GetFlightOffer;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Flights.Api.EndPoints.Offers;

public sealed class GetFlightOfferEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/offers/{offerToken}", async (
            string offerToken,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetFlightOfferQuery(offerToken), cancellationToken);

            return result is null
                ? Results.NotFound(ApiResponseHelper.Error("پیشنهاد پرواز یافت نشد یا منقضی شده است.", statusCode: StatusCodes.Status404NotFound))
                : Results.Ok(ApiResponseHelper.Success(result, "پیشنهاد پرواز با موفقیت دریافت شد."));
        })
        .WithName("Flights.Offers.Get")
        .WithTags("Flights")
        .Produces<ApiResponse<FlightOfferDto>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }
}
