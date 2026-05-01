using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Commands.Reviews;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Reviews.CreateReview;

public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, CreateReviewResponse>
{
    private readonly IReviewRepository _reviewRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IMediator _mediator;

    public CreateReviewCommandHandler(
        IReviewRepository reviewRepo,
        IProductRepository productRepo,
        IShopProductRepository shopProductRepo,
        IMediator mediator)
    {
        _reviewRepo = reviewRepo;
        _productRepo = productRepo;
        _shopProductRepo = shopProductRepo;
        _mediator = mediator;
    }

    public async Task<CreateReviewResponse> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        // Verify the user has a delivered order from a shop that carries this product
        var (shopProducts, _) = await _shopProductRepo.GetByProductAsync(product.Id, isActive: true, page: 1, pageSize: 1, cancellationToken);
        var shopId = shopProducts.FirstOrDefault()?.ShopId;

        if (shopId.HasValue)
        {
            var hasPurchased = await _mediator.Send(new HasUserPurchasedQuery(
                UserId: request.UserId,
                SourceModule: "Store",
                SourceReferenceId: shopId.Value), cancellationToken);

            if (!hasPurchased)
                throw new StoreDomainException("فقط خریداران می‌توانند نظر ثبت کنند", "PURCHASE_REQUIRED");
        }

        var alreadyReviewed = await _reviewRepo.UserHasReviewedAsync(request.ProductId, request.UserId, cancellationToken);
        if (alreadyReviewed)
            throw new StoreDomainException("شما قبلاً برای این محصول نظر ثبت کرده‌اید", "REVIEW_ALREADY_EXISTS");

        var review = Review.Create(request.ProductId, request.UserId, request.Rating, request.Comment);

        await _reviewRepo.AddAsync(review, cancellationToken);

        return new CreateReviewResponse(review.Id);
    }
}

