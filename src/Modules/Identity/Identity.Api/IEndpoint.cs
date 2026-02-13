using Microsoft.AspNetCore.Routing;

namespace Identity.Api;

public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
