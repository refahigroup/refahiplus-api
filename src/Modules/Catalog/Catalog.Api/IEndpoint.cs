using Microsoft.AspNetCore.Routing;

namespace Catalog.Api;

public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
