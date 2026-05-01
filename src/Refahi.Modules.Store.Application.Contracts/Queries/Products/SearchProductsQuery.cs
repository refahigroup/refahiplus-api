using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Products;

public sealed record SearchProductsQuery(
    int ModuleId,
    string Query,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<ProductsPagedResponse>;
