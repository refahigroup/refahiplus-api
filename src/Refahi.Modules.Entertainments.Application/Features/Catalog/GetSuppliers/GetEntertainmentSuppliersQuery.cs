using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;

namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetSuppliers;

public sealed record GetEntertainmentSuppliersQuery(int PageNumber = 1, int PageSize = 24, string? ProviderKey = null,
    string? LocationCode = null, string? CategoryCode = null) : IRequest<EntertainmentSupplierPage>;

