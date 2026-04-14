using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Reviews;
using Refahi.Modules.Store.Application.Contracts.Queries.Reviews;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Reviews.GetProductReviews;

public class GetProductReviewsQueryHandler : IRequestHandler<GetProductReviewsQuery, ProductReviewsResponse>
{
    private readonly IReviewRepository _reviewRepo;

    public GetProductReviewsQueryHandler(IReviewRepository reviewRepo)
        => _reviewRepo = reviewRepo;

    public async Task<ProductReviewsResponse> Handle(GetProductReviewsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _reviewRepo.GetPagedAsync(
            request.ProductId,
            approvedOnly: true,
            page: request.PageNumber,
            pageSize: request.PageSize,
            cancellationToken);

        var averageRating = await _reviewRepo.GetAverageRatingAsync(request.ProductId, cancellationToken);

        var dtos = items.Select(r => new ReviewDto(r.Id, r.UserId, r.Rating, r.Comment, r.CreatedAt));

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        return new ProductReviewsResponse(dtos, averageRating, total, request.PageNumber, request.PageSize, totalPages);
    }
}
