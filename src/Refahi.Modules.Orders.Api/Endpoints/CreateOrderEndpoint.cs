using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Orders.Api.Endpoints;

public class CreateOrderEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder) return;
    }
}
