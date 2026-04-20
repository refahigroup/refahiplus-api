using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Reviews;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Reviews.ApproveReview;

public class ApproveReviewCommandHandler : IRequestHandler<ApproveReviewCommand, ApproveReviewResponse>
{
    private readonly IReviewRepository _reviewRepo;

    public ApproveReviewCommandHandler(IReviewRepository reviewRepo)
    {
        _reviewRepo = reviewRepo;
    }

    public async Task<ApproveReviewResponse> Handle(ApproveReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviewRepo.GetByIdAsync(request.ReviewId, cancellationToken)
            ?? throw new StoreDomainException("نظر یافت نشد", "REVIEW_NOT_FOUND");

        review.Approve();

        await _reviewRepo.UpdateAsync(review, cancellationToken);

        return new ApproveReviewResponse(review.Id, review.IsApproved);
    }
}
