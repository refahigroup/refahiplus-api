using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Application.Contracts.Abstraction;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Products;

public sealed class GetSellersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet(
                "/sellers",
                async (ICommerceProvider provider, CancellationToken cancellationToken) =>
                {
                    var result = await provider.GetSellersAsync(cancellationToken);

                    return Results.Ok(
                        ApiResponseHelper.Success(result, "‌اطلاعات با موفقیت دریافت شدند.")
                    );
                }
            )
            .WithName("Commerce.Sellers")
            .WithTags("Commerce")
            .Produces<ApiResponse<IEnumerable<Seller>>>(StatusCodes.Status200OK);
    }
}
