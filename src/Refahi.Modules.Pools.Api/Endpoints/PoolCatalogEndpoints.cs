using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Shared.Presentation;
using static Refahi.Modules.Pools.Api.Endpoints.PoolEndpointResponse;
namespace Refahi.Modules.Pools.Api.Endpoints;
public sealed class PoolCatalogEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        Catalog(routes.MapGet("/catalog/pools", (string? search, string? state, string? city, int? gender, string? sort, int? page, int? limit, IMediator m, CancellationToken ct) =>
            Run(() => m.Send(new ListPoolVenuesQuery(new(search, state, city, gender, sort ?? "name", page ?? 1, limit ?? 10)), ct))), "Catalog.Pools");
        Catalog(routes.MapGet("/catalog/pools/{id:guid}", (Guid id, IMediator m, CancellationToken ct) => Run(() => m.Send(new GetPoolVenueQuery(id), ct))), "Catalog.Pool");
        Catalog(routes.MapGet("/catalog/sessions", (string? search, string? state, string? city, int? gender, string? sort, int? page, int? limit, Guid? poolId, DateOnly? date, IMediator m, CancellationToken ct) =>
            Run(() => m.Send(new ListPoolSessionsQuery(new(search, state, city, gender, sort ?? "date", page ?? 1, limit ?? 10, poolId, date)), ct))), "Catalog.Sessions");
        Catalog(routes.MapGet("/catalog/sessions/{id:guid}", (Guid id, IMediator m, CancellationToken ct) => Run(() => m.Send(new GetPoolSessionQuery(id), ct))), "Catalog.Session");
        Catalog(routes.MapGet("/catalog/states", (IMediator m, CancellationToken ct) => Run(() => m.Send(new GetPoolLocationsQuery(null, false), ct))), "Catalog.States");
        Catalog(routes.MapGet("/catalog/cities", (string? state, IMediator m, CancellationToken ct) => Run(() => m.Send(new GetPoolLocationsQuery(state, true), ct))), "Catalog.Cities");
        Catalog(routes.MapPost("/quotes", (QuoteRequest r, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new QuotePoolPurchaseQuery(r.Choices), ct)); }), "Quote");
    }
}
public sealed record QuoteRequest(IReadOnlyList<PoolChoice> Choices);
