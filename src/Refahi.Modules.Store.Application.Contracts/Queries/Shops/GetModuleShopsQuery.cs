using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Shops;

/// <summary>Returns paginated active shops that have at least one valid product in the StoreModule's category.</summary>
public sealed record GetModuleShopsQuery(
    int ModuleId,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<ShopsPagedResponse>;
