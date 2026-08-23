using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Application.Contracts.Abstraction;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Offers;

public sealed class GetOffersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet(
                "/offers",
                async (ICommerceProvider provider, CancellationToken cancellationToken) =>
                {
                    var result = await provider.GetOffersAsync(cancellationToken);

                    return Results.Ok(
                        ApiResponseHelper.Success(result, "‌اطلاعات با موفقیت دریافت شدند.")
                    );
                }
            )
            .WithName("Commerce.Offers")
            .WithTags("Commerce")
            .Produces<ApiResponse<IEnumerable<Offer>>>(StatusCodes.Status200OK);
    }
}
