using System.Reflection;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Orders.Api.Endpoints;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;

namespace Refahi.Modules.Orders.Tests;

public sealed class GetOrderByIdEndpointTests
{
    [Theory]
    [InlineData("User", "Admin", "Admin")]
    [InlineData("Admin", "User", "Admin")]
    [InlineData("Admin", null, "Admin")]
    [InlineData("User", null, "User")]
    public async Task Caller_role_respects_admin_membership_regardless_of_claim_order(
        string firstRole, string? secondRole, string expectedRole)
    {
        var callerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, callerId.ToString()),
            new(ClaimTypes.Role, firstRole)
        };
        if (secondRole is not null)
            claims.Add(new Claim(ClaimTypes.Role, secondRole));

        var mediator = DispatchProxy.Create<IMediator, RecordingMediator>();
        var recorder = (RecordingMediator)mediator;
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(mediator);
        await using var app = builder.Build();
        new GetOrderByIdEndpoint().Map(app.MapGroup("/api/orders"));
        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .Single(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "Orders.GetOrderById");
        var context = new DefaultHttpContext
        {
            RequestServices = app.Services,
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };
        context.Request.Method = "GET";
        context.Request.RouteValues["orderId"] = orderId.ToString();
        context.SetEndpoint(endpoint);

        await endpoint.RequestDelegate!(context);

        Assert.NotNull(recorder.Query);
        Assert.Equal(orderId, recorder.Query.OrderId);
        Assert.Equal(callerId, recorder.Query.CallerUserId);
        Assert.Equal(expectedRole, recorder.Query.CallerRole);
    }

    public class RecordingMediator : DispatchProxy
    {
        public GetOrderByIdQuery? Query { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (args?[0] is GetOrderByIdQuery query)
            {
                Query = query;
                return Task.FromResult<OrderDto?>(null);
            }

            throw new NotSupportedException();
        }
    }
}
