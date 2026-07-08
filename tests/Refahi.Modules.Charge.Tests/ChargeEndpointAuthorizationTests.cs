using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Refahi.Modules.Charge.Api.Endpoints;

namespace Refahi.Modules.Charge.Tests;

public sealed class ChargeEndpointAuthorizationTests
{
    [Fact]
    public void Only_charge_discovery_endpoints_are_public()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddLogging();
        builder.Services.AddMediatR(typeof(CatalogEndpoints).Assembly);
        var app = builder.Build(); var group = app.MapGroup("/api/charge");
        new CatalogEndpoints().Map(group);
        new ChargeRequestEndpoints().Map(group);
        new SecureChargeRequestEndpoints().Map(group);
        new AdminEndpoints().Map(group);

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToList();
        Assert.NotEmpty(endpoints);

        var publicNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Charge.Catalog.Operators",
            "Charge.Catalog.Products",
            "Charge.Catalog.Offers",
            "Charge.Catalog.PostpaidBalance",
            "Charge.Catalog.PinCategories",
            "Charge.Catalog.Quote"
        };

        foreach (var endpoint in endpoints)
        {
            var name = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
            if (name is null || !name.StartsWith("Charge.", StringComparison.Ordinal))
                continue;

            var authorization = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            if (publicNames.Contains(name))
                Assert.Empty(authorization);
            else
                Assert.NotEmpty(authorization);
        }
    }
}
