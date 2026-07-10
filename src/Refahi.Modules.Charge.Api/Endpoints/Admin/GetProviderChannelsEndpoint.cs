using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class GetProviderChannelsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("admin/provider/channels", async (ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await sender.Send(new GetProviderChannelsQuery(), ct))))
            .RequireAuthorization("AdminOnly")
            .WithName("Charge.Admin.ProviderChannels")
            .WithTags("Charge.Admin")
            .Produces<ApiResponse<IReadOnlyList<ProviderChannelDto>>>(StatusCodes.Status200OK);
    }
}
