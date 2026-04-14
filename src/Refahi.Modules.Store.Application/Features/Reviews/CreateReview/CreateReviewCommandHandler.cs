using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Reviews;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Reviews.CreateReview;

public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, CreateReviewResponse>
{
    private readonly IReviewRepository _reviewRepo;
    private readonly IProductRepository _productRepo;

    public CreateReviewCommandHandler(IReviewRepository reviewRepo, IProductRepository productRepo)
    {
        _reviewRepo = reviewRepo;
        _productRepo = productRepo;
    }

    public async Task<CreateReviewResponse> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        var alreadyReviewed = await _reviewRepo.UserHasReviewedAsync(request.ProductId, request.UserId, cancellationToken);
        if (alreadyReviewed)
            throw new StoreDomainException("شما قبلاً برای این محصول نظر ثبت کرده‌اید", "REVIEW_ALREADY_EXISTS");

        var review = Review.Create(request.ProductId, request.UserId, request.Rating, request.Comment);

        await _reviewRepo.AddAsync(review, cancellationToken);

        return new CreateReviewResponse(review.Id);
    }
}
