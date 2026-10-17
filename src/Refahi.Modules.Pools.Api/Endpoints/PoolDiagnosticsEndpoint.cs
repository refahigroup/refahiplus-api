using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Shared.Presentation;
using static Refahi.Modules.Pools.Api.Endpoints.PoolEndpointResponse;
namespace Refahi.Modules.Pools.Api.Endpoints;
public sealed class PoolDiagnosticsEndpoint:IEndpoint
{
    public void Map(object app)
    {
        if(app is not IEndpointRouteBuilder routes)return;
        Named(routes.MapGet("/admin/catalog-status",(HttpContext h,IMediator m,CancellationToken ct)=>{NoStore(h);return Run(()=>m.Send(new GetPoolCatalogDiagnosticsQuery(),ct));}),"Catalog.Diagnostics").RequireAuthorization("AdminOnly");
    }
}
