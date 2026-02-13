using Microsoft.AspNetCore.Routing;

namespace Orders.Api;

public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
