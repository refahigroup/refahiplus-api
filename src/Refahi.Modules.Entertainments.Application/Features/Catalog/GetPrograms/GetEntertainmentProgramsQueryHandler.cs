using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetPrograms;
public sealed class GetEntertainmentProgramsQueryHandler(IEntertainmentContentReader content) : IRequestHandler<GetEntertainmentProgramsQuery, EntertainmentCatalogPage>
{
    public Task<EntertainmentCatalogPage> Handle(GetEntertainmentProgramsQuery request, CancellationToken ct) => content.ProgramsAsync(request.Search, request.SupplierKey, request.PageNumber, request.PageSize, request.LocationCode, request.CategoryCode, ct);
}
