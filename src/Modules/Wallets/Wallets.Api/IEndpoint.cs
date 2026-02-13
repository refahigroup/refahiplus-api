using Microsoft.AspNetCore.Routing;

namespace Wallets.Api;

public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
