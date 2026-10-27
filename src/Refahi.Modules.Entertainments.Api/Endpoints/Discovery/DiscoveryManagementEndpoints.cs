using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Api.Endpoints.Cart;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
using Refahi.Modules.Entertainments.Application.Features.Discovery;
using Refahi.Shared.Presentation;
namespace Refahi.Modules.Entertainments.Api.Endpoints.Discovery;

public sealed class DiscoveryManagementEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapGet("/admin/location-mappings", async (IEntertainmentDiscovery discovery, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await discovery.MappingsAsync(ct))))
            .RequireAuthorization("AdminOnly").WithName("Entertainments.LocationMappings").WithTags("Entertainments.Admin");
        routes.MapPut("/admin/location-mappings", async (MappingBody body, HttpContext context, IMediator sender, CancellationToken ct) => {
            if (!_Helpers.TryUser(context, out var actor)) return _Helpers.Unauthorized();
            await sender.Send(new SetEntertainmentLocationMappingCommand(body.Code, body.CityId, actor), ct);
            return Results.Ok(ApiResponseHelper.Success(true));
        }).RequireAuthorization("AdminOnly").WithName("Entertainments.SetLocationMapping").WithTags("Entertainments.Admin");
        routes.MapGet("/legacy/programs/{nativeId:guid}", async (Guid nativeId, IEntertainmentDiscovery discovery, CancellationToken ct) => {
            var key = await discovery.ResolveProgramKeyAsync(nativeId.ToString(), ct);
            return key.HasValue ? Results.Ok(ApiResponseHelper.Success(key.Value)) : Results.NotFound(ApiResponseHelper.Error("خدمت یافت نشد", statusCode: 404));
        }).WithName("Entertainments.ResolveLegacyProgram").WithTags("Entertainments.Discovery");
    }
    public sealed record MappingBody(string Code, int CityId);
}
