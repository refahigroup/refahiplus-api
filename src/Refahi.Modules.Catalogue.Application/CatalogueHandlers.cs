using MediatR;
using Refahi.Modules.Catalogue.Application.Contracts;
namespace Refahi.Modules.Catalogue.Application;
public sealed class CatalogueHandlers(CatalogueService service):IRequestHandler<SearchCatalogueQuery,CatalogueResponse>,IRequestHandler<GetCatalogueCitiesQuery,IReadOnlyList<CatalogueCity>>,IRequestHandler<GetCatalogueLocationsQuery,CatalogueLocationsResponse>
{
 public Task<CatalogueResponse> Handle(SearchCatalogueQuery request,CancellationToken ct)=>service.SearchAsync(request.Query,ct);
 public Task<IReadOnlyList<CatalogueCity>> Handle(GetCatalogueCitiesQuery request,CancellationToken ct)=>service.CitiesAsync(ct);
 public Task<CatalogueLocationsResponse> Handle(GetCatalogueLocationsQuery request,CancellationToken ct)=>service.LocationsAsync(ct);
}
