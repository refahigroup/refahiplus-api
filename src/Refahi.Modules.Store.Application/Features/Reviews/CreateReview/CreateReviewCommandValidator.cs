using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Reviews;

namespace Refahi.Modules.Store.Application.Features.Reviews.CreateReview;

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("شناسه محصول الزامی است");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("شناسه کاربر الزامی است");

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5).WithMessage("امتیاز باید بین ۱ تا ۵ باشد");

        RuleFor(x => x.Comment)
            .MaximumLength(1000).WithMessage("متن نظر نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد")
            .When(x => x.Comment is not null);
    }
}
