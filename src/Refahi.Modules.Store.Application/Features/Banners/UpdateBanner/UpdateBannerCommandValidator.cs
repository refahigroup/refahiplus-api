using FluentValidation;
using Refahi.Modules.Store.Application.Contracts.Commands.Banners;

namespace Refahi.Modules.Store.Application.Features.Banners.UpdateBanner;

public class UpdateBannerCommandValidator : AbstractValidator<UpdateBannerCommand>
{
    public UpdateBannerCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("شناسه بنر الزامی است");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان بنر الزامی است")
            .MaximumLength(200).WithMessage("عنوان بنر نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد");

        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("آدرس تصویر الزامی است");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("ترتیب نمایش نمی‌تواند منفی باشد");
    }
}
