using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetSuppliers;
public sealed record GetEntertainmentSuppliersQuery(int PageNumber = 1, int PageSize = 24, string? LocationCode = null, string? CategoryCode = null) : IRequest<EntertainmentSupplierPage>;
