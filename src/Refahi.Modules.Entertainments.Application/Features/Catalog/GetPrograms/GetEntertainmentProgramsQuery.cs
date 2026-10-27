using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;

namespace Refahi.Modules.Entertainments.Application.Features.Catalog.GetPrograms;

public sealed record GetEntertainmentProgramsQuery(
    string? Search, 
    string? ProviderKey, 
    string? SupplierKey,
    int PageNumber = 1, 
    int PageSize = 24,
    string? LocationCode = null,
    string? CategoryCode = null
) : IRequest<EntertainmentCatalogPage>;

