using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Reviews;

namespace Refahi.Modules.Store.Application.Contracts.Queries.Reviews;

public sealed record GetProductReviewsQuery(
    Guid ProductId, int PageNumber = 1, int PageSize = 10
) : IRequest<ProductReviewsResponse>;

public sealed record ProductReviewsResponse(
    IEnumerable<ReviewDto> Data,
    double AverageRating, int TotalCount,
    int PageNumber, int PageSize, int TotalPages);
