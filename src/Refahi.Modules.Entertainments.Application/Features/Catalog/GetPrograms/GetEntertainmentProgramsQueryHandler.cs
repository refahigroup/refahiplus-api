using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Requests;
using Refahi.Shared.Services.Cache;

namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetPrograms;

public sealed class GetEntertainmentProgramsQueryHandler(IEntertainmentProviderFactory providers) :
    IRequestHandler<GetEntertainmentProgramsQuery, EntertainmentCatalogPage>
{
    public async Task<EntertainmentCatalogPage> Handle(GetEntertainmentProgramsQuery request, CancellationToken ct)
    {
        return await providers.GetRequired(request.ProviderKey ?? "touristpanel").GetProgramsAsync(
            new(request.Search, request.ProviderKey, request.SupplierKey, request.PageNumber, request.PageSize, request.LocationCode, request.CategoryCode), ct);
    }
}
