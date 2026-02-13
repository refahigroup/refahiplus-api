using Microsoft.AspNetCore.Routing;

namespace Organizations.Api;

public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
