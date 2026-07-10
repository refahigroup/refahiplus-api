using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class GetProviderErrorsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("admin/provider/errors", async (ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await sender.Send(new GetProviderErrorsQuery(), ct))))
            .RequireAuthorization("AdminOnly")
            .WithName("Charge.Admin.ProviderErrors")
            .WithTags("Charge.Admin")
            .Produces<ApiResponse<IReadOnlyList<ProviderErrorDto>>>(StatusCodes.Status200OK);
    }
}
