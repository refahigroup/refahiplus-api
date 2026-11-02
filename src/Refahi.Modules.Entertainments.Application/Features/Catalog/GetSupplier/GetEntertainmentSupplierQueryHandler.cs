using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetSupplier;
public sealed class GetEntertainmentSupplierQueryHandler(IEntertainmentContentReader content) : IRequestHandler<GetEntertainmentSupplierQuery, EntertainmentSupplierDto?>
{
    public Task<EntertainmentSupplierDto?> Handle(GetEntertainmentSupplierQuery request, CancellationToken ct) => content.SupplierAsync(request.SupplierKey, ct);
}
