using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetPrograms;
public sealed record GetEntertainmentProgramsQuery(string? Search, string? SupplierKey, int PageNumber = 1, int PageSize = 24, string? LocationCode = null, string? CategoryCode = null) : IRequest<EntertainmentCatalogPage>;
