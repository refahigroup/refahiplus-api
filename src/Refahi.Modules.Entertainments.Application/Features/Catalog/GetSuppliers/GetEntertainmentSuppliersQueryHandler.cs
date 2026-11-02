using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetSuppliers;
public sealed class GetEntertainmentSuppliersQueryHandler(IEntertainmentContentReader content) : IRequestHandler<GetEntertainmentSuppliersQuery, EntertainmentSupplierPage>
{
    public Task<EntertainmentSupplierPage> Handle(GetEntertainmentSuppliersQuery request, CancellationToken ct) => content.SuppliersAsync(request.PageNumber, request.PageSize, request.LocationCode, request.CategoryCode, ct);
}
