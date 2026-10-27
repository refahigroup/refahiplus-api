using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Application.Features.Catalog.GetSupplier;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Catalog;

public sealed class GetSupplierEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/suppliers/{providerKey}/{supplierKey}", async (
                string providerKey,
                string supplierKey,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var supplier = await mediator.Send(new GetEntertainmentSupplierQuery(providerKey, supplierKey), ct);
                return supplier is null
                    ? Results.NotFound(ApiResponseHelper.Error("فروشنده یافت نشد", statusCode: 404))
                    : Results.Ok(ApiResponseHelper.Success(supplier));
            })
            .WithName("Entertainment.GetSupplier")
            .WithTags("Entertainment.Catalog");
    }
}
