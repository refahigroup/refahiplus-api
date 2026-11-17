using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Refahi.Modules.Bus.Application.Contracts;
using Refahi.Modules.Bus.Application.Discovery;
using Refahi.Modules.Bus.Infrastructure;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Bus.Api;

public static class BusModule
{
    public static IServiceCollection RegisterBusModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddMediatR(typeof(BusDiscoveryHandler).Assembly);
        services.AddValidatorsFromAssemblyContaining<BusSearchValidator>();
        return services.RegisterBusInfrastructure(configuration);
    }

    public static WebApplication UseBusModule(this WebApplication app)
    {
        var routes = app.MapGroup("/api/bus");
        if (app.Configuration.GetValue<bool>("Bus:Enabled"))
        {
            app.Services.MigrateBus();
            new BusEndpoints().Map(routes);
        }
        return app;
    }

}

public sealed class BusEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapGet("/locations", (string? q, ISender sender, HttpContext http, CancellationToken ct) =>
            Send(new BusLocationsQuery(q), sender, http, ct)).WithName("Bus.Locations").WithTags("Bus");

        routes.MapGet("/search", ([AsParameters] SearchParameters query, ISender sender, HttpContext http, CancellationToken ct) =>
            Send(new BusSearchQuery(query.OriginCityId, query.DestinationCityId, query.DepartureDate,
                query.Company, query.BusType, query.FromTime, query.Sort, query.Page ?? 1), sender, http, ct))
            .WithName("Bus.Search").WithTags("Bus");

        routes.MapGet("/offers/{token:guid}", (Guid token, ISender sender, HttpContext http, CancellationToken ct) =>
            Send(new BusOfferQuery(token), sender, http, ct)).WithName("Bus.Offer").WithTags("Bus");
    }

    private static async Task<IResult> Send<T>(IRequest<T> request, ISender sender, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        try
        {
            return Results.Ok(ApiResponseHelper.Success(await sender.Send(request, ct)));
        }
        catch (BusRequestException ex)
        {
            return Results.Json(ApiResponseHelper.Error(ex.Message, statusCode: ex.StatusCode), statusCode: ex.StatusCode);
        }
    }

    public sealed record SearchParameters([FromQuery] int OriginCityId, [FromQuery] int DestinationCityId,
        [FromQuery] DateOnly DepartureDate, [FromQuery] string? Company, [FromQuery] string? BusType,
        [FromQuery] TimeOnly? FromTime, [FromQuery] string? Sort, [FromQuery] int? Page);
}
