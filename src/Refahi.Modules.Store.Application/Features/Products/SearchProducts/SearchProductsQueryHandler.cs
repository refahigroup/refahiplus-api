using MediatR;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;

namespace Refahi.Modules.Store.Application.Features.Products.SearchProducts;

public sealed class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, ProductsPagedResponse>
{
    private readonly IMediator _mediator;

    public SearchProductsQueryHandler(IMediator mediator) => _mediator = mediator;

    public Task<ProductsPagedResponse> Handle(SearchProductsQuery request, CancellationToken ct)
        => _mediator.Send(new GetProductsQuery(
            request.ModuleId,
            request.Query,
            "newest",
            request.PageNumber,
            Math.Min(request.PageSize, 30)), ct);
}
